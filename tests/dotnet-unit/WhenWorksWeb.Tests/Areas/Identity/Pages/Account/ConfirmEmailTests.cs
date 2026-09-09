using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 3 end-to-end tests for ConfirmEmail.cshtml.cs -- the page RegisterModel/ResendEmailConfirmationModel/
/// ExternalLoginModel (via <c>EmailConfirmationLinkSender</c>) and Manage/Email.cshtml.cs's
/// <c>OnPostSendVerificationEmailAsync</c> all link to, but which was never actually scaffolded (see
/// Spec/Bugs/BUGS-missing-email-confirmation-pages.ospec). Runs through the real Identity pipeline and,
/// where possible, follows the actual emailed link captured by <see cref="TestEmailSender"/> rather than
/// stopping at token generation -- the exact gap the spec calls out in the pre-fix test suite.
/// </summary>
public class ConfirmEmailTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Str0ng!Pass";

    private readonly CustomWebApplicationFactory _factory;

    public ConfirmEmailTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    /// <summary>
    /// Registers a real account through the actual Register page (rather than seeding one directly), so the
    /// confirmation link this test follows is the exact one <c>EmailConfirmationLinkSender</c> built and
    /// <see cref="TestEmailSender"/> captured -- not a hand-built substitute.
    /// </summary>
    private async Task<(string Email, string ConfirmationLink)> RegisterAsync(HttpClient client, string userName)
    {
        var getResponse = await client.GetAsync("/Identity/Account/Register");
        var html = await getResponse.Content.ReadAsStringAsync();
        var token = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(html);

        var email = $"{userName}@example.com";
        var response = await client.PostAsync("/Identity/Account/Register", new FormUrlEncodedContent(
        [
            new("Input.UserName", userName),
            new("Input.Email", email),
            new("Input.DisplayName", userName),
            new("Input.Color", "ff66c4"),
            new("Input.Password", Password),
            new("Input.ConfirmPassword", Password),
            new("__RequestVerificationToken", token)
        ]));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var sent = Assert.Single(_factory.EmailSender.SentEmails, e => e.Email == email);
        var match = System.Text.RegularExpressions.Regex.Match(sent.HtmlMessage, "href='([^']*ConfirmEmail[^']*)'");
        Assert.True(match.Success, $"No ConfirmEmail link found in: {sent.HtmlMessage}");

        return (email, System.Net.WebUtility.HtmlDecode(match.Groups[1].Value));
    }

    /// <summary>
    /// Following the real, emailed confirmation link must confirm the account (not just return 200) and the
    /// account must then be able to sign in -- <see cref="LoginTests"/> already proves an unconfirmed account
    /// is blocked, so this is the other half of that guarantee.
    /// </summary>
    [Fact]
    public async Task ConfirmEmail_WithValidLinkFromRegistration_ConfirmsAccountAndAllowsSignIn()
    {
        var client = CreateClient();
        var (_, confirmationLink) = await RegisterAsync(client, "confirmlinkuser");

        var confirmResponse = await client.GetAsync(confirmationLink);

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmHtml = await confirmResponse.Content.ReadAsStringAsync();
        Assert.Contains("Thank you for confirming your email", confirmHtml);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync("confirmlinkuser");
        Assert.True(await userManager.IsEmailConfirmedAsync(user!));

        // Confirms the whole point of the fix end-to-end: a previously-blocked sign-in now succeeds.
        var loginGetResponse = await client.GetAsync("/Identity/Account/Login");
        var loginHtml = await loginGetResponse.Content.ReadAsStringAsync();
        var loginToken = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(loginHtml);
        var loginResponse = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(
        [
            new("UserName", "confirmlinkuser"),
            new("Password", Password),
            new("__RequestVerificationToken", loginToken)
        ]));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.DoesNotContain("RegisterConfirmation", loginResponse.Headers.Location?.OriginalString);
    }

    /// <summary>
    /// A tampered/invalid token must fail gracefully -- a rendered error message, not an unhandled exception
    /// bubbling up as a 500 -- and must leave the account unconfirmed.
    /// </summary>
    [Fact]
    public async Task ConfirmEmail_WithTamperedCode_ShowsErrorAndLeavesAccountUnconfirmed()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUserBuilder().WithUserName("tamperedcodeuser").WithEmail("tamperedcodeuser@example.com").Build();
        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var client = CreateClient();
        var response = await client.GetAsync($"/Identity/Account/ConfirmEmail?userId={user.Id}&code=not-a-real-token");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Error confirming your email", html);

        var reloaded = await userManager.FindByIdAsync(user.Id);
        Assert.False(await userManager.IsEmailConfirmedAsync(reloaded!));
    }

    /// <summary>
    /// A userId that doesn't correspond to any account must 404, not throw or silently succeed --
    /// <c>FindByIdAsync</c> returning null is the expected "no such user" case, handled explicitly.
    /// </summary>
    [Fact]
    public async Task ConfirmEmail_WithUnknownUserId_ReturnsNotFound()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/Identity/Account/ConfirmEmail?userId=00000000-0000-0000-0000-000000000000&code=anything");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Missing <c>code</c> (or <c>userId</c>) must fail with a clear 400, not attempt to confirm with a null
    /// token and not hit the stock scaffold's <c>RedirectToPage("/Index")</c> -- this app has no such page,
    /// so that branch would otherwise throw <see cref="InvalidOperationException"/> the first time a real
    /// malformed link reached it (see the guard's remarks in ConfirmEmail.cshtml.cs).
    /// </summary>
    [Fact]
    public async Task ConfirmEmail_WithMissingCode_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUserBuilder().WithUserName("missingcodeuser").WithEmail("missingcodeuser@example.com").Build();
        await userManager.CreateAsync(user, Password);

        var client = CreateClient();

        var response = await client.GetAsync($"/Identity/Account/ConfirmEmail?userId={user.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
