using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Data;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Security;

/// <summary>
/// Tier 3 tests proving <c>[ValidateAntiForgeryToken]</c> is actually enforced by the real pipeline for every
/// state-changing POST action, not just <c>EventsController.Create</c> (already covered by the general Tier 3
/// smoke test). Each test submits a syntactically valid request with no antiforgery token/cookie at all.
/// </summary>
public class AntiForgeryEnforcementTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AntiForgeryEnforcementTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    private async Task<string> SeedEventAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Events.Add(new EventBuilder().WithCode(code).Build());
        await db.SaveChangesAsync();

        return code;
    }

    /// <summary>
    /// Joining an event without an antiforgery token must be rejected, and must not redirect to the event's
    /// sign-in page as if the code were merely invalid.
    /// </summary>
    [Fact]
    public async Task Join_WithoutAntiForgeryToken_IsRejected()
    {
        var code = await SeedEventAsync("BCDFGH");
        var client = CreateClient();

        var response = await client.PostAsync("/Events/Join", new FormUrlEncodedContent(
        [
            new("EventCode", code)
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Signing in to an event without an antiforgery token must be rejected, and no participant must be created.
    /// </summary>
    [Fact]
    public async Task SignIn_WithoutAntiForgeryToken_IsRejectedAndPersistsNoParticipant()
    {
        var code = await SeedEventAsync("MNPQRS");
        var client = CreateClient();

        var response = await client.PostAsync($"/event/{code}/signin", new FormUrlEncodedContent(
        [
            new("Code", code),
            new("DisplayName", "Should Never Exist"),
            new("Color", "ff66c4")
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Participants.AnyAsync(p => p.DisplayName == "Should Never Exist"));
    }

    /// <summary>
    /// Deleting an event without an antiforgery token must be rejected even for an authenticated request that
    /// would otherwise pass the [Authorize] check — isolates antiforgery enforcement from authentication so a
    /// missing/invalid token can't be masked by also failing to be logged in.
    /// </summary>
    [Fact]
    public async Task MyEventsDelete_WithoutAntiForgeryToken_IsRejectedEvenWhenAuthenticated()
    {
        using var seedScope = _factory.Services.CreateScope();
        var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUserBuilder().WithUserName("deleter").WithEmail("deleter@example.com").Build();
        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded);

        var evt = new EventBuilder().WithCode("TVWXYZ").WithCreatedByUserId(user.Id).Build();
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id);

        var response = await client.PostAsync("/MyEvents/Delete", new FormUrlEncodedContent(
        [
            new("eventId", evt.Id.ToString()),
            new("deleteMode", "event")
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await verifyDb.Events.AnyAsync(e => e.Code == "TVWXYZ"));
    }
}
