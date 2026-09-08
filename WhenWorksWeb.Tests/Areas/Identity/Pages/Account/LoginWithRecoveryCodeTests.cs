using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 3 end-to-end tests for LoginWithRecoveryCode.cshtml.cs's <c>OnPostAsync</c> explicit
/// <c>IsLockedOutAsync</c> check, added alongside Login.cshtml.cs's <c>lockoutOnFailure: true</c>
/// flip (see Spec/Features/FEATURES-enforce-account-lockout.ospec). That check exists because the
/// base <c>SignInManager.TwoFactorRecoveryCodeSignInAsync</c> never consults lockout on its own --
/// unlike <c>TwoFactorAuthenticatorSignInAsync</c> (covered by <see cref="LoginWith2faTests"/>), a
/// wrong recovery code never counts towards lockout, and -- the more serious half of the gap this
/// page's check closes -- without it, a *valid* recovery code would sign a still-locked-out account in
/// anyway, bypassing the lockout everywhere else on this flow enforces. Setup here locks the account
/// out mid-flow (after the password step, before the recovery-code submission) since an account that's
/// already locked out before the password step never reaches this page at all -- ApplicationSignInManager's
/// overridden <c>CheckPasswordSignInAsync</c> redirects to Lockout directly from Login.cshtml.cs first.
/// </summary>
public class LoginWithRecoveryCodeTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Str0ng!Pass";

    private readonly CustomWebApplicationFactory _factory;

    public LoginWithRecoveryCodeTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    /// <summary>
    /// Creates a confirmed, 2FA-enabled user with a known valid recovery code, not yet locked out --
    /// the account is locked out mid-test instead, after the password step, to reach the page's own
    /// check rather than the earlier one in Login.cshtml.cs (see this class's remarks).
    /// </summary>
    private async Task<string> CreateTwoFactorUserWithRecoveryCodeAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        user.EmailConfirmed = true;

        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var twoFactorResult = await userManager.SetTwoFactorEnabledAsync(user, true);
        Assert.True(twoFactorResult.Succeeded, string.Join("; ", twoFactorResult.Errors.Select(e => e.Description)));

        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 2);
        Assert.NotNull(recoveryCodes);
        return recoveryCodes!.First();
    }

    private async Task LockOutUserAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        var lockoutResult = await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(30));
        Assert.True(lockoutResult.Succeeded, string.Join("; ", lockoutResult.Errors.Select(e => e.Description)));
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string userName, string password)
    {
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
    /// The bypass this page's check closes: a valid recovery code against an account locked out after
    /// the password step must still redirect to Lockout, not sign the user in. Before the fix, this
    /// succeeded and redirected to <c>ReturnUrl</c> instead -- see this class's remarks.
    /// </summary>
    [Fact]
    public async Task LoginWithRecoveryCode_WithValidCodeAgainstLockedOutAccount_RedirectsToLockoutInsteadOfSigningIn()
    {
        var recoveryCode = await CreateTwoFactorUserWithRecoveryCodeAsync("recoverycodebypassuser");
        var client = CreateClient();

        var passwordResponse = await PostLoginAsync(client, "recoverycodebypassuser", Password);
        Assert.Equal(HttpStatusCode.Redirect, passwordResponse.StatusCode);

        await LockOutUserAsync("recoverycodebypassuser");

        var recoveryPageUrl = "/Identity/Account/LoginWithRecoveryCode";
        var recoveryPageHtml = await (await client.GetAsync(recoveryPageUrl)).Content.ReadAsStringAsync();
        var token = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(recoveryPageHtml);

        var codeResponse = await client.PostAsync(recoveryPageUrl, new FormUrlEncodedContent(
        [
            new("RecoveryCode", recoveryCode),
            new("__RequestVerificationToken", token)
        ]));

        Assert.Equal(HttpStatusCode.Redirect, codeResponse.StatusCode);
        Assert.EndsWith("/Identity/Account/Lockout", codeResponse.Headers.Location?.ToString());
    }

    /// <summary>
    /// The point of this page's own <c>LockedOutUserName</c> assignment (distinct from
    /// Login.cshtml.cs's and LoginWith2fa.cshtml.cs's identically-named properties): the Lockout page
    /// reached from here must display this account's own remaining time, not fall back to the generic
    /// "try again in a few minutes".
    /// </summary>
    [Fact]
    public async Task LoginWithRecoveryCode_WhenLockedOut_LockoutPageShowsTimeRemaining()
    {
        var recoveryCode = await CreateTwoFactorUserWithRecoveryCodeAsync("recoverycodelockouttimeuser");
        var client = CreateClient();

        await PostLoginAsync(client, "recoverycodelockouttimeuser", Password);
        await LockOutUserAsync("recoverycodelockouttimeuser");

        var recoveryPageUrl = "/Identity/Account/LoginWithRecoveryCode";
        var recoveryPageHtml = await (await client.GetAsync(recoveryPageUrl)).Content.ReadAsStringAsync();
        var token = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(recoveryPageHtml);

        var codeResponse = await client.PostAsync(recoveryPageUrl, new FormUrlEncodedContent(
        [
            new("RecoveryCode", recoveryCode),
            new("__RequestVerificationToken", token)
        ]));
        Assert.EndsWith("/Identity/Account/Lockout", codeResponse.Headers.Location?.ToString());

        var lockoutPageResponse = await client.GetAsync("/Identity/Account/Lockout");

        Assert.Equal(HttpStatusCode.OK, lockoutPageResponse.StatusCode);
        var html = await lockoutPageResponse.Content.ReadAsStringAsync();
        Assert.Contains("minute", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("try again in a few minutes", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Contrast case: a wrong recovery code against an account that isn't locked out still falls
    /// through to the generic "Invalid recovery code entered" message, same as before this page's
    /// lockout check was added -- proves the new check only intercepts actually-locked-out accounts,
    /// not every submission.
    /// </summary>
    [Fact]
    public async Task LoginWithRecoveryCode_WithWrongCodeAndNoLockout_ShowsGenericInvalidCodeMessage()
    {
        await CreateTwoFactorUserWithRecoveryCodeAsync("recoverycodewrongnolockoutuser");
        var client = CreateClient();

        await PostLoginAsync(client, "recoverycodewrongnolockoutuser", Password);

        var recoveryPageUrl = "/Identity/Account/LoginWithRecoveryCode";
        var recoveryPageHtml = await (await client.GetAsync(recoveryPageUrl)).Content.ReadAsStringAsync();
        var token = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(recoveryPageHtml);

        // Within ModelConstants.RecoveryCodeMaxLength (20) so it reaches OnPostAsync's own
        // TwoFactorRecoveryCodeSignInAsync call instead of failing StringLength validation first.
        var codeResponse = await client.PostAsync(recoveryPageUrl, new FormUrlEncodedContent(
        [
            new("RecoveryCode", "wrong-code12"),
            new("__RequestVerificationToken", token)
        ]));

        Assert.Equal(HttpStatusCode.OK, codeResponse.StatusCode);
        var html = await codeResponse.Content.ReadAsStringAsync();
        Assert.Contains("Invalid recovery code entered", html, StringComparison.OrdinalIgnoreCase);
    }
}
