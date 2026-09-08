using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using WhenWorksWeb.Tests.Fixtures;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 3 end-to-end test for Register.cshtml.cs's <c>OnPostAsync</c> against the real, configured
/// <c>IdentityOptions.SignIn.RequireConfirmedAccount = true</c> (see IdentityConfiguration.cs).
/// Guards against Spec/Bugs/BUGS-registration-confirmation-redirect.ospec's original crash: that
/// branch used to redirect to a "RegisterConfirmation" page that had never actually been scaffolded
/// in this project, so every successful registration threw
/// <c>InvalidOperationException: No page named 'RegisterConfirmation' matches the supplied values</c>
/// instead of completing. Runs through the real Identity pipeline (real antiforgery token, real page
/// routing) rather than calling the page model directly, since a missing/misnamed page is a routing
/// failure that a direct PageModel call can't detect.
/// </summary>
public class RegisterTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Str0ng!Pass";

    private readonly CustomWebApplicationFactory _factory;

    public RegisterTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    /// <summary>
    /// Registering with correct, valid input must complete with no server error and land on
    /// RegisterConfirmation -- the exact scenario that used to throw.
    /// </summary>
    [Fact]
    public async Task Register_WithValidInput_RedirectsToRegisterConfirmation()
    {
        var client = CreateClient();

        // WebApplicationFactoryClientOptions.HandleCookies defaults to true, so the antiforgery
        // cookie set by this GET is captured and replayed automatically on the POST below --
        // matching LoginTests' PostLoginAsync pattern.
        var getResponse = await client.GetAsync("/Identity/Account/Register");
        var html = await getResponse.Content.ReadAsStringAsync();
        var token = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(html);

        var response = await client.PostAsync("/Identity/Account/Register", new FormUrlEncodedContent(
        [
            new("Input.UserName", "newregistrant"),
            new("Input.Email", "newregistrant@example.com"),
            new("Input.DisplayName", "New Registrant"),
            new("Input.Color", "ff66c4"),
            new("Input.Password", Password),
            new("Input.ConfirmPassword", Password),
            new("__RequestVerificationToken", token)
        ]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString;
        Assert.NotNull(location);
        Assert.StartsWith("/Identity/Account/RegisterConfirmation", location);
        var queryString = location![location.IndexOf('?')..];
        var query = QueryHelpers.ParseQuery(queryString);
        Assert.Equal("newregistrant@example.com", query["email"]);

        // Confirms the page it redirects to actually exists and renders -- the original bug's
        // failure mode (InvalidOperationException: No page named 'RegisterConfirmation') would
        // have already surfaced on the redirect above, but following through also catches a page
        // that resolves as a route yet throws while rendering.
        using var followClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var confirmationResponse = await followClient.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, confirmationResponse.StatusCode);
        var confirmationHtml = await confirmationResponse.Content.ReadAsStringAsync();
        Assert.Contains("newregistrant@example.com", confirmationHtml);
    }
}
