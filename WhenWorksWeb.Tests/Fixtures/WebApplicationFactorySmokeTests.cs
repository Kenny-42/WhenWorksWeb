using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Data;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Tier 3 smoke tests: the small, deliberately limited set of full-pipeline tests for behavior that only the
/// real HTTP pipeline exercises (routing, antiforgery, Identity authorization) — everything else stays in the
/// Tier 2 controller tests. See CODING_CONVENTIONS.md's Testing Conventions for why this tier stays short.
/// </summary>
public class WebApplicationFactorySmokeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WebApplicationFactorySmokeTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates a client using an <c>https://</c> base address. TestServer never opens a real socket — this is
    /// purely so <see cref="System.Net.CookieContainer"/> treats the connection as secure and actually resends
    /// the event access cookie on follow-up requests, since <c>SetEventAccessCookie</c> correctly marks it
    /// <c>Secure = true</c>. Without this, the cookie round-trip silently breaks: <c>CookieContainer</c> stores
    /// a <c>Secure</c> cookie fine but refuses to attach it to requests against an <c>http://</c> base address,
    /// which manifested as an inexplicable redirect back to sign-in after a successful sign-in POST.
    /// </summary>
    private HttpClient CreateClient(bool allowAutoRedirect = true) => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = allowAutoRedirect
    });

    /// <summary>
    /// End-to-end: create an event, join it by code from a second "browser," and sign in as a new
    /// participant — proving routing, model binding, antiforgery, EF Core (via the SQLite swap), and the
    /// event access cookie all work together through the real pipeline, not just individually in isolation.
    /// </summary>
    [Fact]
    public async Task CreateJoinAndSignIn_FullFlow_Succeeds()
    {
        // Client 1 creates the event.
        var creatorClient = CreateClient();

        var homePageHtml = await creatorClient.GetStringAsync("/");
        var createToken = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(homePageHtml);

        var createResponse = await creatorClient.PostAsync("/Events/Create", new FormUrlEncodedContent(
        [
            new("CreateEventName", "Smoke Test Event"),
            new("__RequestVerificationToken", createToken)
        ]));
        createResponse.EnsureSuccessStatusCode(); // auto-redirects (default client) to the new event's sign-in page
        Assert.EndsWith("/signin", createResponse.RequestMessage!.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);

        var eventCode = createResponse.RequestMessage.RequestUri.AbsolutePath.Split('/')[2];

        // Client 2 (a different "browser" — its own cookie container) joins by code.
        var joinerClient = CreateClient();

        var joinerHomeHtml = await joinerClient.GetStringAsync("/");
        var joinToken = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(joinerHomeHtml);

        var joinResponse = await joinerClient.PostAsync("/Events/Join", new FormUrlEncodedContent(
        [
            new("EventCode", eventCode),
            new("__RequestVerificationToken", joinToken)
        ]));
        joinResponse.EnsureSuccessStatusCode();
        Assert.EndsWith($"/event/{eventCode}/signin", joinResponse.RequestMessage!.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);

        var signInPageHtml = await joinResponse.Content.ReadAsStringAsync();
        var signInToken = AntiForgeryTokenExtractor.ExtractRequestVerificationToken(signInPageHtml);

        var signInResponse = await joinerClient.PostAsync($"/event/{eventCode}/signin", new FormUrlEncodedContent(
        [
            new("Code", eventCode),
            new("DisplayName", "Smoke Tester"),
            new("Color", "ff66c4"),
            new("__RequestVerificationToken", signInToken)
        ]));
        signInResponse.EnsureSuccessStatusCode();
        Assert.Equal($"/event/{eventCode}", signInResponse.RequestMessage!.RequestUri!.AbsolutePath, ignoreCase: true);

        var eventHomeHtml = await signInResponse.Content.ReadAsStringAsync();
        Assert.Contains("Smoke Test Event", eventHomeHtml, StringComparison.Ordinal);

        // Confirm the participant was actually persisted, not just that the page happened to render.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var savedParticipant = await db.Participants.SingleAsync(p => p.DisplayName == "Smoke Tester");
        Assert.Equal("ff66c4", savedParticipant.Color);
    }

    /// <summary>
    /// An anonymous request to an Admin-area page should be challenged (redirected to the login page), not
    /// served or met with a generic error — proves Identity authentication/authorization middleware is
    /// actually wired into the pipeline.
    /// </summary>
    [Fact]
    public async Task AdminPage_WhenAnonymous_RedirectsToLogin()
    {
        var client = CreateClient(allowAutoRedirect: false);

        var response = await client.GetAsync("/Admin/Users/ManageUsers");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A state-changing POST submitted without a valid antiforgery token must be rejected outright, proving
    /// [ValidateAntiForgeryToken] is actually enforced by the real pipeline rather than only present in source.
    /// </summary>
    [Fact]
    public async Task CreateEvent_WithoutAntiForgeryToken_IsRejected()
    {
        var client = CreateClient();

        var response = await client.PostAsync("/Events/Create", new FormUrlEncodedContent(
        [
            new("CreateEventName", "Should Never Be Created")
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Events.AnyAsync(e => e.Title == "Should Never Be Created"));
    }
}
