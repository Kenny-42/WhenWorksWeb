using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 3 end-to-end tests for LoginWith2fa.cshtml.cs's <c>OnPostAsync</c> <c>IsLockedOut</c> branch,
/// added alongside Login.cshtml.cs's <c>lockoutOnFailure: true</c> flip (see
/// Spec/Features/FEATURES-enforce-account-lockout.ospec). <see cref="LoginWith2faModelInputTests"/>
/// already covers <c>TwoFactorCode</c>'s own validation attributes; this file is about what happens
/// once a wrong code at this stage crosses the lockout threshold -- particularly that this page's own
/// <c>LockedOutUserName</c> assignment (distinct from Login.cshtml.cs's identically-named property)
/// actually reaches the Lockout page it redirects to.
/// </summary>
public class LoginWith2faTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Str0ng!Pass";

    private readonly CustomWebApplicationFactory _factory;

    public LoginWith2faTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    /// <summary>
    /// Creates a confirmed, 2FA-enabled user already one wrong attempt short of
    /// <c>IdentityOptions.Lockout.MaxFailedAccessAttempts</c> (5, see IdentityConfiguration.cs) -- so a
    /// single wrong code entered at the LoginWith2fa stage is enough to cross the threshold, rather
    /// than looping through several intermediate requests the way
    /// <c>LoginTests.Login_WithFiveFailedAttempts_LocksAccountAndRedirectsToLockout</c> drives it
    /// through wrong passwords. This count survives the correct-password step below unreset --
    /// <see cref="ApplicationSignInManagerTests.CheckPasswordSignInAsync_WithCorrectPasswordAndUnrememberedTwoFactorAccount_DoesNotResetLockoutCounter"/>
    /// is why: a 2FA-enabled account's counter is only reset once sign-in fully completes, not at the
    /// password step.
    /// </summary>
    private async Task CreateTwoFactorUserOneAttemptFromLockoutAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        user.EmailConfirmed = true;

        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var twoFactorResult = await userManager.SetTwoFactorEnabledAsync(user, true);
        Assert.True(twoFactorResult.Succeeded, string.Join("; ", twoFactorResult.Errors.Select(e => e.Description)));

        var maxAttempts = userManager.Options.Lockout.MaxFailedAccessAttempts;
        for (var i = 0; i < maxAttempts - 1; i++)
        {
            await userManager.AccessFailedAsync(user);
        }
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string userName, string password)
    {
        // WebApplicationFactoryClientOptions.HandleCookies defaults to true, so the antiforgery cookie
        // set by this GET, and the two-factor-user-id cookie set by the POST below, are captured and
        // replayed automatically on the requests that follow -- matching LoginTests' pattern.
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
    /// Drives the full flow: the correct password reaches the 2FA challenge without locking the
    /// account out (RequiresTwoFactor, not IsLockedOut -- a correct password on a 2FA account neither
    /// increments nor resets the counter), then a single wrong code there crosses the threshold this
    /// test set up one attempt away from, locking the account out and redirecting to Lockout instead
    /// of redisplaying an "Invalid authenticator code" error.
    /// </summary>
    [Fact]
    public async Task LoginWith2fa_WithWrongCodeReachingMaxFailedAccessAttempts_LocksOutAndRedirectsToLockout()
    {
        await CreateTwoFactorUserOneAttemptFromLockoutAsync("twofactorlockoutuser");
        var client = CreateClient();

        var passwordResponse = await PostLoginAsync(client, "twofactorlockoutuser", Password);
        Assert.Equal(HttpStatusCode.Redirect, passwordResponse.StatusCode);
        var twoFactorPageUrl = passwordResponse.Headers.Location!.ToString();
        Assert.StartsWith("/Identity/Account/LoginWith2fa", twoFactorPageUrl);

        var twoFactorPageHtml = await (await client.GetAsync(twoFactorPageUrl)).Content.ReadAsStringAsync();
        var token = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(twoFactorPageHtml);

        var codeResponse = await client.PostAsync(twoFactorPageUrl, new FormUrlEncodedContent(
        [
            new("TwoFactorCode", "000000"),
            new("__RequestVerificationToken", token)
        ]));

        Assert.Equal(HttpStatusCode.Redirect, codeResponse.StatusCode);
        Assert.EndsWith("/Identity/Account/Lockout", codeResponse.Headers.Location?.ToString());
    }

    /// <summary>
    /// The point of the <c>LockedOutUserName</c> assignment added alongside the branch above: the
    /// Lockout page reached from here must display this account's own remaining time, exactly as
    /// <c>LoginTests.Login_WhenLockedOut_LockoutPageShowsTimeRemaining</c> pins for the password path
    /// -- proving LoginWith2fa.cshtml.cs's own assignment (not just Login.cshtml.cs's) actually reaches
    /// the Lockout page, rather than it falling back to the generic "try again in a few minutes".
    /// </summary>
    [Fact]
    public async Task LoginWith2fa_WhenLockedOut_LockoutPageShowsTimeRemaining()
    {
        await CreateTwoFactorUserOneAttemptFromLockoutAsync("twofactorlockouttimeuser");
        var client = CreateClient();

        var passwordResponse = await PostLoginAsync(client, "twofactorlockouttimeuser", Password);
        var twoFactorPageUrl = passwordResponse.Headers.Location!.ToString();
        var twoFactorPageHtml = await (await client.GetAsync(twoFactorPageUrl)).Content.ReadAsStringAsync();
        var token = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(twoFactorPageHtml);

        var codeResponse = await client.PostAsync(twoFactorPageUrl, new FormUrlEncodedContent(
        [
            new("TwoFactorCode", "000000"),
            new("__RequestVerificationToken", token)
        ]));
        Assert.EndsWith("/Identity/Account/Lockout", codeResponse.Headers.Location?.ToString());

        var lockoutPageResponse = await client.GetAsync("/Identity/Account/Lockout");

        Assert.Equal(HttpStatusCode.OK, lockoutPageResponse.StatusCode);
        var html = await lockoutPageResponse.Content.ReadAsStringAsync();
        Assert.Contains("minute", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("try again in a few minutes", html, StringComparison.OrdinalIgnoreCase);
    }
}
