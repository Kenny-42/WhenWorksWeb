using Microsoft.AspNetCore.Mvc;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Controllers;

/// <summary>
/// Tier 2 tests for <see cref="WhenWorksWeb.Controllers.EventsController"/>'s Home action
/// (<c>EventsController.Home.cs</c>), including the access-cookie flow it shares with SignIn
/// (<c>EventsController.AccessCookie.cs</c>).
/// </summary>
public class EventsControllerHomeTests : EventsControllerTestFixture
{
    /// <summary>
    /// A code with no matching event should return the shared "not found" page.
    /// </summary>
    [Fact]
    public async Task Home_WithNonExistentCode_ReturnsEventNotFoundView()
    {
        var (controller, _) = CreateController();

        var result = await controller.Home("ZZZZZZ", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Error.cshtml", viewResult.ViewName);
    }

    /// <summary>
    /// An existing event with no access cookie on the request should redirect to sign-in rather than showing
    /// event details to an unauthenticated visitor.
    /// </summary>
    [Fact]
    public async Task Home_WithExistingEventAndNoAccessCookie_RedirectsToSignIn()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSignIn", redirect.RouteName);
    }

    /// <summary>
    /// After a full sign-in round trip (real cookie protect on the SignIn response, real unprotect on a
    /// subsequent Home request), the home page should render with the signed-in participant's rejoin code —
    /// proving the access cookie mechanism works end to end, not just that SignIn "returns a redirect."
    /// </summary>
    [Fact]
    public async Task Home_WithValidAccessCookieFromRealSignIn_ReturnsViewWithParticipantRejoinCode()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").WithTitle("Trivia Night").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (signInController, signInHttpContext) = CreateController();
        var signInModel = new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4" };
        await signInController.SignIn("BCDFGH", signInModel, CancellationToken.None);

        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, "WhenWorksWeb.EventAccess.BCDFGH");
        Assert.NotNull(cookieValue);

        var (homeController, _) = CreateController(
            requestCookies: new Dictionary<string, string> { ["WhenWorksWeb.EventAccess.BCDFGH"] = cookieValue! });

        var result = await homeController.Home("BCDFGH", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<EventHomeViewModel>(viewResult.Model);
        Assert.Equal("BCDFGH", model.Code);
        Assert.Equal("Trivia Night", model.Title);

        var savedParticipant = Assert.Single(Db.Participants);
        Assert.Equal(savedParticipant.RejoinCode, model.RejoinCode);
    }

    /// <summary>
    /// A tampered/corrupted access cookie must not crash the request — it should be treated as no cookie at
    /// all (redirect to sign-in), and the invalid cookie should be deleted from the response.
    /// </summary>
    [Fact]
    public async Task Home_WithTamperedAccessCookie_RedirectsToSignInAndDeletesCookie()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (controller, httpContext) = CreateController(
            requestCookies: new Dictionary<string, string> { ["WhenWorksWeb.EventAccess.BCDFGH"] = "not-a-real-protected-value" });

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSignIn", redirect.RouteName);

        var setCookieHeaders = httpContext.Response.Headers.SetCookie.ToArray();
        Assert.Contains(setCookieHeaders, h => h!.StartsWith("WhenWorksWeb.EventAccess.BCDFGH=", StringComparison.Ordinal)
            && h.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }
}
