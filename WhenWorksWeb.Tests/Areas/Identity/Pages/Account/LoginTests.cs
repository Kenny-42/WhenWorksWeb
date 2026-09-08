using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 3 end-to-end tests for Login.cshtml.cs's <c>OnPostAsync</c>, specifically the
/// <c>SignInResult.IsNotAllowed</c> branch added alongside
/// <c>IdentityOptions.SignIn.RequireConfirmedAccount = true</c> (see
/// Spec/Features/FEATURES-email-verification.ospec and
/// WhenWorksWeb.Tests/Models/IdentityConfigurationTests.cs), and the password-first ordering
/// (now centralized in <see cref="ApplicationSignInManager"/>, see
/// Spec/Refactors/REFACTOR-signin-manager-and-confirmation-email.ospec) that prevents that branch,
/// and lockout, from doubling as a username-enumeration oracle. Runs through the real Identity
/// pipeline (real antiforgery token, real <see cref="SignInManager{TUser}"/>) rather than calling
/// the page model directly, since the behavior under test is the interaction between
/// <c>RequireConfirmedAccount</c>/lockout and the page -- not just the page's own branching logic.
/// </summary>
public class LoginTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Str0ng!Pass";

    private readonly CustomWebApplicationFactory _factory;

    public LoginTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private async Task CreateUserAsync(string userName, bool emailConfirmed)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        user.EmailConfirmed = emailConfirmed;

        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>
    /// Creates a confirmed user that's already locked out (matching what repeated failed attempts, or
    /// an admin action, would produce), rather than driving it there through failed login attempts --
    /// this test is about what happens once locked out, not about lockout's own failure-count logic.
    /// </summary>
    private async Task CreateLockedOutUserAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        user.EmailConfirmed = true;

        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        var lockoutResult = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(30));
        Assert.True(lockoutResult.Succeeded, string.Join("; ", lockoutResult.Errors.Select(e => e.Description)));
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string userName, string password)
    {
        // WebApplicationFactoryClientOptions.HandleCookies defaults to true, so the antiforgery
        // cookie set by this GET is captured and replayed automatically on the POST below -- no
        // manual cookie handling needed, matching WebApplicationFactorySmokeTests' pattern.
        var getResponse = await client.GetAsync("/Identity/Account/Login");
        var html = await getResponse.Content.ReadAsStringAsync();
        var token = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(html);

        return await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(
        [
            new("UserName", userName),
            new("Password", password),
            new("__RequestVerificationToken", token)
        ]));
    }

    /// <summary>
    /// An account whose email is unconfirmed must be redirected to RegisterConfirmation with the
    /// correct password -- the whole point of turning on <c>RequireConfirmedAccount</c>, now
    /// surfaced as a dedicated landing page instead of an inline form error (see
    /// RegisterConfirmation.cshtml.cs).
    /// </summary>
    [Fact]
    public async Task Login_WithUnconfirmedEmailAndCorrectPassword_RedirectsToRegisterConfirmation()
    {
        await CreateUserAsync("unconfirmeduser", emailConfirmed: false);
        var client = CreateClient();

        var response = await PostLoginAsync(client, "unconfirmeduser", Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString;
        Assert.NotNull(location);
        Assert.StartsWith("/Identity/Account/RegisterConfirmation", location);
        var query = QueryHelpers.ParseQuery(location![location.IndexOf('?')..]);
        Assert.Equal("unconfirmeduser@example.com", query["email"]);
    }

    /// <summary>
    /// A wrong password against an unconfirmed account must fall into the same generic "Invalid login
    /// attempt" response as any other wrong password -- not the confirm-email redirect. Guards against
    /// the username-enumeration bug this test file was originally written for: PasswordSignInAsync
    /// checks email confirmation BEFORE the password, so calling it directly on a wrong password would
    /// return the same result as a correct one, letting an attacker who doesn't know the password learn
    /// an unconfirmed account exists. Login.cshtml.cs's OnPostAsync now checks the password itself
    /// first specifically to prevent that.
    /// </summary>
    [Fact]
    public async Task Login_WithUnconfirmedEmailAndWrongPassword_ShowsGenericInvalidLoginMessage()
    {
        await CreateUserAsync("unconfirmedwrongpassworduser", emailConfirmed: false);
        var client = CreateClient();

        var response = await PostLoginAsync(client, "unconfirmedwrongpassworduser", "definitely-the-wrong-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid login attempt", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirm your email", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A confirmed account with the correct password must still be let straight through -- proves
    /// <c>RequireConfirmedAccount</c> blocks only unconfirmed accounts, not sign-in generally.
    /// </summary>
    [Fact]
    public async Task Login_WithConfirmedEmailAndCorrectPassword_Succeeds()
    {
        await CreateUserAsync("confirmeduser", emailConfirmed: true);
        var client = CreateClient();

        var response = await PostLoginAsync(client, "confirmeduser", Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    /// <summary>
    /// Once an account is actually locked out, a wrong password against it also redirects to
    /// ./Lockout, same as a correct password would -- an accepted trade-off of turning on
    /// <c>lockoutOnFailure: true</c> (see Spec/Features/FEATURES-enforce-account-lockout.ospec),
    /// not a bug. <see cref="ApplicationSignInManager.CheckPasswordSignInAsync"/>'s wrong-password
    /// branch checks <c>IsLockedOutAsync</c> on every failed attempt when <c>lockoutOnFailure</c> is
    /// true (matching the base <c>SignInManager</c>'s own behavior), so it can't distinguish "this
    /// guess just crossed the threshold" from "this account was already locked" -- and shouldn't try
    /// to: a locked account that kept accepting guesses indistinguishably from an unlocked one would
    /// defeat the point of lockout. Before this feature, with <c>lockoutOnFailure: false</c>, this
    /// case never disclosed lockout at all, which is what this test originally pinned -- see git
    /// history for that version.
    /// </summary>
    [Fact]
    public async Task Login_WithLockedOutAccountAndWrongPassword_RedirectsToLockout()
    {
        await CreateLockedOutUserAsync("lockedoutwrongpassworduser");
        var client = CreateClient();

        var response = await PostLoginAsync(client, "lockedoutwrongpassworduser", "definitely-the-wrong-password");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Identity/Account/Lockout", response.Headers.Location?.ToString());
    }

    /// <summary>
    /// A correct password against a locked-out account must still redirect to ./Lockout -- lockout is
    /// only ever disclosed once the password is proven correct.
    /// </summary>
    [Fact]
    public async Task Login_WithLockedOutAccountAndCorrectPassword_RedirectsToLockout()
    {
        await CreateLockedOutUserAsync("lockedoutcorrectpassworduser");
        var client = CreateClient();

        var response = await PostLoginAsync(client, "lockedoutcorrectpassworduser", Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Identity/Account/Lockout", response.Headers.Location?.ToString());
    }

    /// <summary>
    /// Pins the case where both statuses apply at once. A wrong password never reaches
    /// <c>PreSignInCheck</c> (confirmation) at all -- <see cref="ApplicationSignInManager.CheckPasswordSignInAsync"/>'s
    /// wrong-password branch resolves lockout on its own, before the password is ever proven correct
    /// (see <see cref="Login_WithLockedOutAccountAndWrongPassword_RedirectsToLockout"/>'s remarks for
    /// why that's an accepted trade-off), so it redirects to ./Lockout regardless of confirmation
    /// status. A correct password instead reaches <c>PreSignInCheck</c>, which checks confirmation
    /// before lockout -- see the paired test below.
    /// </summary>
    [Fact]
    public async Task Login_WithLockedOutAndUnconfirmedAccountAndWrongPassword_RedirectsToLockout()
    {
        await CreateLockedOutAndUnconfirmedUserAsync("lockedoutunconfirmedwrongpassworduser");
        var client = CreateClient();

        var response = await PostLoginAsync(client, "lockedoutunconfirmedwrongpassworduser", "definitely-the-wrong-password");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Identity/Account/Lockout", response.Headers.Location?.ToString());
    }

    /// <summary>
    /// See <see cref="Login_WithLockedOutAndUnconfirmedAccountAndWrongPassword_RedirectsToLockout"/>'s
    /// remarks -- the correct-password direction, asserting confirmation wins over lockout.
    /// </summary>
    [Fact]
    public async Task Login_WithLockedOutAndUnconfirmedAccountAndCorrectPassword_RedirectsToRegisterConfirmation()
    {
        await CreateLockedOutAndUnconfirmedUserAsync("lockedoutunconfirmedcorrectpassworduser");
        var client = CreateClient();

        var response = await PostLoginAsync(client, "lockedoutunconfirmedcorrectpassworduser", Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Identity/Account/RegisterConfirmation", response.Headers.Location?.OriginalString);
    }

    /// <summary>
    /// Creates a user that's both locked out and unconfirmed, for the combined test above.
    /// </summary>
    private async Task CreateLockedOutAndUnconfirmedUserAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        user.EmailConfirmed = false;

        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        var lockoutResult = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(30));
        Assert.True(lockoutResult.Succeeded, string.Join("; ", lockoutResult.Errors.Select(e => e.Description)));
    }

    private async Task<int> GetAccessFailedCountAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        return await userManager.GetAccessFailedCountAsync(user!);
    }

    /// <summary>
    /// End-to-end coverage of Login.cshtml.cs's <c>lockoutOnFailure: true</c> flip (see
    /// Spec/Features/FEATURES-enforce-account-lockout.ospec): 4 wrong passwords stay below
    /// <c>IdentityOptions.Lockout.MaxFailedAccessAttempts</c> (5, see IdentityConfiguration.cs) and
    /// each shows the same generic "Invalid login attempt" as before; the 5th crosses the threshold
    /// and redirects to Lockout instead, since <see cref="ApplicationSignInManager.CheckPasswordSignInAsync"/>
    /// itself reports <c>IsLockedOut</c> once the count it just incremented reaches the limit.
    /// </summary>
    [Fact]
    public async Task Login_WithFiveFailedAttempts_LocksAccountAndRedirectsToLockout()
    {
        await CreateUserAsync("fivefailedattemptsuser", emailConfirmed: true);
        var client = CreateClient();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var response = await PostLoginAsync(client, "fivefailedattemptsuser", "definitely-the-wrong-password");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Invalid login attempt", html, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(4, await GetAccessFailedCountAsync("fivefailedattemptsuser"));

        var fifthResponse = await PostLoginAsync(client, "fivefailedattemptsuser", "definitely-the-wrong-password");

        Assert.Equal(HttpStatusCode.Redirect, fifthResponse.StatusCode);
        Assert.EndsWith("/Identity/Account/Lockout", fifthResponse.Headers.Location?.ToString());
    }

    /// <summary>
    /// Once locked out, even the correct password must still be blocked until <c>LockoutEnd</c>
    /// passes -- otherwise the lockout would only ever stop wrong-password guessing, not an attacker
    /// who eventually finds the right password mid-lockout.
    /// </summary>
    [Fact]
    public async Task Login_WhenLockedOutAndStillBeforeLockoutEnd_CorrectPasswordStillRedirectsToLockout()
    {
        await CreateLockedOutUserAsync("stillbeforelockoutenduser");
        var client = CreateClient();

        var response = await PostLoginAsync(client, "stillbeforelockoutenduser", Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Identity/Account/Lockout", response.Headers.Location?.ToString());
    }

    /// <summary>
    /// Once <c>LockoutEnd</c> has passed, the correct password succeeds again and the failed-access
    /// counter resets -- lockout is a temporary cooldown, not a permanent ban. Expiry is simulated by
    /// setting <c>LockoutEnd</c> into the past directly (matching <see cref="CreateLockedOutUserAsync"/>'s
    /// pattern) rather than waiting out the real 5-minute policy.
    /// </summary>
    [Fact]
    public async Task Login_AfterLockoutEndHasPassed_CorrectPasswordSucceedsAndResetsAccessFailedCount()
    {
        await CreateLockedOutUserAsync("afterlockoutenduser");
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync("afterlockoutenduser");
            await userManager.AccessFailedAsync(user!);
            var expiredLockoutResult = await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(-1));
            Assert.True(expiredLockoutResult.Succeeded, string.Join("; ", expiredLockoutResult.Errors.Select(e => e.Description)));
        }
        var client = CreateClient();

        var response = await PostLoginAsync(client, "afterlockoutenduser", Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/Identity/Account/Lockout", response.Headers.Location?.ToString());
        Assert.Equal(0, await GetAccessFailedCountAsync("afterlockoutenduser"));
    }

    /// <summary>
    /// The Lockout page itself: reached via the redirect above, it must look up the account handed
    /// off through <c>LockedOutUserName</c> TempData and show its actual remaining lockout time,
    /// rather than a static message -- see Lockout.cshtml.cs's <c>TimeRemaining</c>.
    /// </summary>
    [Fact]
    public async Task Login_WhenLockedOut_LockoutPageShowsTimeRemaining()
    {
        await CreateLockedOutUserAsync("lockoutpagetimeuser");
        var client = CreateClient();

        var loginResponse = await PostLoginAsync(client, "lockoutpagetimeuser", Password);
        Assert.EndsWith("/Identity/Account/Lockout", loginResponse.Headers.Location?.ToString());

        var lockoutPageResponse = await client.GetAsync("/Identity/Account/Lockout");

        Assert.Equal(HttpStatusCode.OK, lockoutPageResponse.StatusCode);
        var html = await lockoutPageResponse.Content.ReadAsStringAsync();
        Assert.Contains("minute", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("try again in a few minutes", html, StringComparison.OrdinalIgnoreCase);
    }
}
