using Microsoft.AspNetCore.Mvc;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Controllers;

/// <summary>
/// Tier 2 tests for the core (non-partial-split) actions of <see cref="WhenWorksWeb.Controllers.EventsController"/>:
/// <c>Create</c> and <c>Join</c>. See <c>EventsControllerSignInTests</c>/<c>EventsControllerHomeTests</c> for the
/// other partial files, mirroring the controller's own file split.
/// </summary>
public class EventsControllerTests : EventsControllerTestFixture
{
    /// <summary>
    /// A valid Create submission should persist a new event with a generated code and redirect to its sign-in page.
    /// </summary>
    [Fact]
    public async Task Create_WithValidName_PersistsEventAndRedirectsToSignIn()
    {
        var (controller, _) = CreateController();
        var model = new IndexViewModel { CreateEventName = "Board Game Night" };

        var result = await controller.Create(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSignIn", redirect.RouteName);

        var savedEvent = Assert.Single(Db.Events);
        Assert.Equal("Board Game Night", savedEvent.Title);
        Assert.Equal(6, savedEvent.Code.Length);
        Assert.Equal(savedEvent.Code, redirect.RouteValues!["code"]);
    }

    /// <summary>
    /// Create should trim leading/trailing whitespace from the submitted event name before saving.
    /// </summary>
    [Fact]
    public async Task Create_TrimsWhitespaceFromEventName()
    {
        var (controller, _) = CreateController();
        var model = new IndexViewModel { CreateEventName = "  Trimmed Title  " };

        await controller.Create(model, CancellationToken.None);

        var savedEvent = Assert.Single(Db.Events);
        Assert.Equal("Trimmed Title", savedEvent.Title);
    }

    // ---- Create's browser-detected TimeZoneId (see IndexViewModel.TimeZoneId) ----

    [Fact]
    public async Task Create_WithValidBrowserDetectedTimeZoneId_UsesIt()
    {
        var (controller, _) = CreateController();
        var model = new IndexViewModel { CreateEventName = "Board Game Night", TimeZoneId = "America/New_York" };

        await controller.Create(model, CancellationToken.None);

        Assert.Equal("America/New_York", Assert.Single(Db.Events).TimeZoneId);
    }

    [Fact]
    public async Task Create_WithNullTimeZoneId_FallsBackToDefault()
    {
        var (controller, _) = CreateController();
        var model = new IndexViewModel { CreateEventName = "Board Game Night", TimeZoneId = null };

        await controller.Create(model, CancellationToken.None);

        Assert.Equal("UTC", Assert.Single(Db.Events).TimeZoneId);
    }

    /// <summary>
    /// The regression case behind the IsValidTimeZoneId fix: a resolvable-but-non-IANA (Windows
    /// style) id from a browser that somehow reported one is rejected, same as a garbage string —
    /// both fall back to the default rather than being stored as-is.
    /// </summary>
    [Theory]
    [InlineData("Eastern Standard Time")]
    [InlineData("Not/A/Real/Zone")]
    [InlineData("")]
    public async Task Create_WithInvalidTimeZoneId_FallsBackToDefault(string invalidTimeZoneId)
    {
        var (controller, _) = CreateController();
        var model = new IndexViewModel { CreateEventName = "Board Game Night", TimeZoneId = invalidTimeZoneId };

        await controller.Create(model, CancellationToken.None);

        Assert.Equal("UTC", Assert.Single(Db.Events).TimeZoneId);
    }

    /// <summary>
    /// An empty event name should fail validation and redisplay the Home/Index form without saving anything.
    /// </summary>
    [Fact]
    public async Task Create_WithEmptyName_RedisplaysFormAndPersistsNothing()
    {
        var (controller, _) = CreateController();
        var model = new IndexViewModel { CreateEventName = "   " };
        controller.ModelState.AddModelError(nameof(IndexViewModel.CreateEventName), "Event name is required.");

        var result = await controller.Create(model, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Home/Index.cshtml", viewResult.ViewName);
        Assert.Empty(Db.Events);
    }

    /// <summary>
    /// A Join submission with a code matching an existing event should redirect to that event's sign-in page,
    /// regardless of the case the user typed the code in.
    /// </summary>
    [Fact]
    public async Task Join_WithExistingCode_RedirectsToSignIn()
    {
        var existingEvent = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(existingEvent);
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();
        var model = new IndexViewModel { EventCode = "bcdfgh" };

        var result = await controller.Join(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSignIn", redirect.RouteName);
        Assert.Equal("BCDFGH", redirect.RouteValues!["code"]);
    }

    /// <summary>
    /// A Join submission for a code with no matching event should redisplay the form with a model error and
    /// must not throw or redirect.
    /// </summary>
    [Fact]
    public async Task Join_WithNonExistentCode_RedisplaysFormWithModelError()
    {
        var (controller, _) = CreateController();
        var model = new IndexViewModel { EventCode = "ZZZZZZ" };

        var result = await controller.Join(model, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Home/Index.cshtml", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState[nameof(IndexViewModel.EventCode)]!.Errors,
            e => e.ErrorMessage == "No event was found for that code.");
    }
}
