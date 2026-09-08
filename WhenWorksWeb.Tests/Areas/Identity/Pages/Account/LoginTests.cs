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
    /// A wrong password against a locked-out account must fall into the same generic "Invalid login
    /// attempt" response as any other wrong password -- not the ./Lockout redirect. This is the
    /// gap the pre-<see cref="ApplicationSignInManager"/> fix left open (see that class's remarks and
    /// Spec/Refactors/REFACTOR-signin-manager-and-confirmation-email.ospec's motivation): the old
    /// hand-rolled version deliberately checked lockout independently of the password specifically so
    /// this case would still redirect to ./Lockout, which is exactly the oracle this refactor closes
    /// -- an attacker who doesn't know the password should never learn a locked-out account exists.
    /// </summary>
    [Fact]
    public async Task Login_WithLockedOutAccountAndWrongPassword_ShowsGenericInvalidLoginMessage()
    {
        await CreateLockedOutUserAsync("lockedoutwrongpassworduser");
        var client = CreateClient();

        var response = await PostLoginAsync(client, "lockedoutwrongpassworduser", "definitely-the-wrong-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid login attempt", html, StringComparison.OrdinalIgnoreCase);
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
    /// Pins the case where both statuses apply at once: a wrong password must still show the generic
    /// message (neither status disclosed), while a correct password must resolve to whichever of the
    /// two <see cref="ApplicationSignInManager.CheckPasswordSignInAsync"/>'s <c>PreSignInCheck</c> call
    /// checks first -- confirmation before lockout, matching the framework's own <c>PreSignInCheck</c>
    /// ordering (left untouched by this refactor).
    /// </summary>
    [Fact]
    public async Task Login_WithLockedOutAndUnconfirmedAccountAndWrongPassword_ShowsGenericInvalidLoginMessage()
    {
        await CreateLockedOutAndUnconfirmedUserAsync("lockedoutunconfirmedwrongpassworduser");
        var client = CreateClient();

        var response = await PostLoginAsync(client, "lockedoutunconfirmedwrongpassworduser", "definitely-the-wrong-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid login attempt", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// See <see cref="Login_WithLockedOutAndUnconfirmedAccountAndWrongPassword_ShowsGenericInvalidLoginMessage"/>'s
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
}
