using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 3 end-to-end tests for ConfirmEmailChange.cshtml.cs -- the page Manage/Email.cshtml.cs's
/// <c>OnPostChangeEmailAsync</c> links to, but which was never actually scaffolded (see
/// Spec/Bugs/BUGS-missing-email-confirmation-pages.ospec). Follows the real, emailed change-email link
/// captured by <see cref="TestEmailSender"/> rather than stopping at token generation.
/// </summary>
public class ConfirmEmailChangeTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Str0ng!Pass";

    private readonly CustomWebApplicationFactory _factory;

    public ConfirmEmailChangeTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private async Task<ApplicationUser> CreateConfirmedUserAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        user.EmailConfirmed = true;
        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        return user;
    }

    /// <summary>
    /// Drives the real Manage/Email "change email" POST (authenticated via <c>X-Test-UserId</c>, matching
    /// AntiForgeryEnforcementTests' pattern) to obtain the exact change-email link
    /// <c>EmailModel.OnPostChangeEmailAsync</c> built and <see cref="TestEmailSender"/> captured, rather than
    /// hand-building one.
    /// </summary>
    private async Task<string> RequestEmailChangeAsync(HttpClient client, ApplicationUser user, string newEmail)
    {
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id);

        var getResponse = await client.GetAsync("/Identity/Account/Manage/Email");
        var html = await getResponse.Content.ReadAsStringAsync();
        var token = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(html);

        var response = await client.PostAsync("/Identity/Account/Manage/Email?handler=ChangeEmail", new FormUrlEncodedContent(
        [
            new("Input.NewEmail", newEmail),
            new("__RequestVerificationToken", token)
        ]));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var sent = Assert.Single(_factory.EmailSender.SentEmails, e => e.Email == newEmail);
        var match = System.Text.RegularExpressions.Regex.Match(sent.HtmlMessage, "href='([^']*ConfirmEmailChange[^']*)'");
        Assert.True(match.Success, $"No ConfirmEmailChange link found in: {sent.HtmlMessage}");

        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    /// <summary>
    /// Following the real, emailed change-email link must actually apply the new address -- the exact gap
    /// this spec closes (a link that previously 404'd, per BUGS-missing-email-confirmation-pages.ospec).
    /// </summary>
    [Fact]
    public async Task ConfirmEmailChange_WithValidLink_AppliesNewEmail()
    {
        var user = await CreateConfirmedUserAsync("changeemailuser");
        var client = CreateClient();
        var confirmationLink = await RequestEmailChangeAsync(client, user, "changeemailuser-new@example.com");

        // A fresh, unauthenticated client -- following the emailed link doesn't require the browser that
        // requested the change still being signed in.
        var followClient = CreateClient();
        var confirmResponse = await followClient.GetAsync(confirmationLink);

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmHtml = await confirmResponse.Content.ReadAsStringAsync();
        Assert.Contains("Thank you for confirming your email change", confirmHtml);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var reloaded = await userManager.FindByIdAsync(user.Id);
        Assert.Equal("changeemailuser-new@example.com", await userManager.GetEmailAsync(reloaded!));

        // UserName must stay untouched -- this app's UserName is independent of Email (see
        // ConfirmEmailChange.cshtml.cs's remarks), unlike the stock scaffold this page is adapted from.
        Assert.Equal("changeemailuser", await userManager.GetUserNameAsync(reloaded!));
    }

    /// <summary>
    /// A tampered/invalid token must fail gracefully and leave the account's email untouched.
    /// </summary>
    [Fact]
    public async Task ConfirmEmailChange_WithTamperedCode_ShowsErrorAndLeavesEmailUnchanged()
    {
        var user = await CreateConfirmedUserAsync("tamperedchangeuser");
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/Identity/Account/ConfirmEmailChange?userId={user.Id}&email=someone-else@example.com&code=not-a-real-token");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Error changing email", html);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var reloaded = await userManager.FindByIdAsync(user.Id);
        Assert.Equal("tamperedchangeuser@example.com", await userManager.GetEmailAsync(reloaded!));
    }

    /// <summary>
    /// A userId that doesn't correspond to any account must 404, not throw.
    /// </summary>
    [Fact]
    public async Task ConfirmEmailChange_WithUnknownUserId_ReturnsNotFound()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            "/Identity/Account/ConfirmEmailChange?userId=00000000-0000-0000-0000-000000000000&email=someone@example.com&code=anything");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Missing <c>email</c> (or <c>userId</c>/<c>code</c>) must fail with a clear 400 rather than hitting the
    /// stock scaffold's nonexistent "/Index" page redirect -- see ConfirmEmailChange.cshtml.cs's guard.
    /// </summary>
    [Fact]
    public async Task ConfirmEmailChange_WithMissingEmail_ReturnsBadRequest()
    {
        var user = await CreateConfirmedUserAsync("missingemailparamuser");
        var client = CreateClient();

        var response = await client.GetAsync($"/Identity/Account/ConfirmEmailChange?userId={user.Id}&code=anything");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
