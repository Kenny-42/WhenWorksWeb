using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    /// subsequent Home request), the home page should render with the event's details — proving the access
    /// cookie mechanism works end to end, not just that SignIn "returns a redirect."
    /// </summary>
    [Fact]
    public async Task Home_WithValidAccessCookieFromRealSignIn_ReturnsViewWithEventDetails()
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
        Assert.Equal("BCDFGH", model.Header.Code);
        Assert.Equal("Trivia Night", model.Header.Title);

        Assert.Single(Db.Participants);
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

    /// <summary>
    /// With no availability marks anywhere in the event, the calendar's sparse Dates list must be empty
    /// rather than, say, one entry per participant or per day — there's nothing to render yet.
    /// </summary>
    [Fact]
    public async Task Home_WithNoAvailabilityMarks_ReturnsEmptyCalendarDates()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (signInController, signInHttpContext) = CreateController();
        await signInController.SignIn("BCDFGH", new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4" }, CancellationToken.None);
        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, "WhenWorksWeb.EventAccess.BCDFGH")!;

        var (controller, _) = CreateController(requestCookies: new Dictionary<string, string> { ["WhenWorksWeb.EventAccess.BCDFGH"] = cookieValue });

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventHomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Calendar.Dates);
        Assert.Single(model.Calendar.Participants);
    }

    /// <summary>
    /// The calendar's Dates list must be sparse — only calendar days that have at least one participant
    /// available appear, carrying exactly that set of participant ids and no others.
    /// </summary>
    [Fact]
    public async Task Home_WithAvailabilityMarks_ReturnsOnlyDatesWithPicksAndCorrectParticipantIds()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (signInController, signInHttpContext) = CreateController();
        await signInController.SignIn("BCDFGH", new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4" }, CancellationToken.None);
        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, "WhenWorksWeb.EventAccess.BCDFGH")!;

        var alice = await Db.Participants.SingleAsync(p => p.DisplayName == "Alice");

        var pickedDate = new EventDateBuilder().ForEvent(evt).WithDate(new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero)).Build();
        var unpickedDate = new EventDateBuilder().ForEvent(evt).WithDate(new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero)).Build();
        Db.EventDates.AddRange(pickedDate, unpickedDate);
        await Db.SaveChangesAsync();

        Db.ParticipantAvailabilities.Add(new ParticipantAvailability { ParticipantId = alice.Id, EventDateId = pickedDate.Id });
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController(requestCookies: new Dictionary<string, string> { ["WhenWorksWeb.EventAccess.BCDFGH"] = cookieValue });

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventHomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        var onlyDate = Assert.Single(model.Calendar.Dates);
        Assert.Equal(new DateOnly(2026, 8, 28), onlyDate.Date);
        Assert.Equal([alice.Id], onlyDate.ParticipantIds);
    }

    /// <summary>
    /// The navigable month window is a generous, fixed sanity bound (10 years either way) around today —
    /// not a data-volume optimization (see EventsController.Home.cs's own remarks).
    /// </summary>
    [Fact]
    public async Task Home_CalendarWindow_Spans10YearsBeforeAndAfterTheCurrentMonth()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (signInController, signInHttpContext) = CreateController();
        await signInController.SignIn("BCDFGH", new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4" }, CancellationToken.None);
        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, "WhenWorksWeb.EventAccess.BCDFGH")!;

        var (controller, _) = CreateController(requestCookies: new Dictionary<string, string> { ["WhenWorksWeb.EventAccess.BCDFGH"] = cookieValue });

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventHomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var expectedInitialMonth = new DateOnly(today.Year, today.Month, 1);

        Assert.Equal(expectedInitialMonth, model.Calendar.InitialMonth);
        Assert.Equal(expectedInitialMonth.AddMonths(-120), model.Calendar.WindowStartMonth);
        Assert.Equal(expectedInitialMonth.AddMonths(120), model.Calendar.WindowEndMonth);
    }

    /// <summary>
    /// Every participant in the event appears in the calendar's Participants list (for the legend), ordered
    /// alphabetically by display name — not just organizers, and not just ones who've picked a date.
    /// </summary>
    [Fact]
    public async Task Home_Calendar_IncludesEveryParticipantOrderedByDisplayName()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (signInController, signInHttpContext) = CreateController();
        await signInController.SignIn("BCDFGH", new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Zack", Color = "ff66c4" }, CancellationToken.None);
        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, "WhenWorksWeb.EventAccess.BCDFGH")!;

        Db.Participants.Add(new ParticipantBuilder().ForEvent(evt).WithDisplayName("Amy").WithColor("111111").Build());
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController(requestCookies: new Dictionary<string, string> { ["WhenWorksWeb.EventAccess.BCDFGH"] = cookieValue });

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventHomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(["Amy", "Zack"], model.Calendar.Participants.Select(p => p.DisplayName));
    }

    /// <summary>
    /// With no organizer-chosen final dates on the event, the calendar's FinalDates list must be
    /// empty rather than, say, null or throwing — the Availability tab's live "Final dates" card
    /// relies on an empty (not missing) list to know it should render nothing.
    /// </summary>
    [Fact]
    public async Task Home_WithNoFinalDates_ReturnsEmptyCalendarFinalDates()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (signInController, signInHttpContext) = CreateController();
        await signInController.SignIn("BCDFGH", new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4" }, CancellationToken.None);
        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, "WhenWorksWeb.EventAccess.BCDFGH")!;

        var (controller, _) = CreateController(requestCookies: new Dictionary<string, string> { ["WhenWorksWeb.EventAccess.BCDFGH"] = cookieValue });

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventHomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Calendar.FinalDates);
    }

    /// <summary>
    /// The calendar's FinalDates list carries every EventFinalDate row for the event, ordered by
    /// start date, so the Availability tab's live "Final dates" card shows the same entries as
    /// the Finalize tab in the same order.
    /// </summary>
    [Fact]
    public async Task Home_WithFinalDates_ReturnsThemOrderedByStartDate()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        Db.EventFinalDates.AddRange(
            new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 9, 10) },
            new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 8, 28), EndDate = new DateOnly(2026, 8, 30) });
        await Db.SaveChangesAsync();

        var (signInController, signInHttpContext) = CreateController();
        await signInController.SignIn("BCDFGH", new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4" }, CancellationToken.None);
        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, "WhenWorksWeb.EventAccess.BCDFGH")!;

        var (controller, _) = CreateController(requestCookies: new Dictionary<string, string> { ["WhenWorksWeb.EventAccess.BCDFGH"] = cookieValue });

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventHomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(
            [new DateOnly(2026, 8, 28), new DateOnly(2026, 9, 10)],
            model.Calendar.FinalDates.Select(f => f.StartDate));
        Assert.Equal(new DateOnly(2026, 8, 30), model.Calendar.FinalDates[0].EndDate);
    }

    // ---- CalendarSnapshot (reconnect catch-up endpoint for live-sync — see event-live-sync.js) ----

    [Fact]
    public async Task CalendarSnapshot_WithNonExistentEventCode_ReturnsNotFound()
    {
        var (controller, _) = CreateController();

        var result = await controller.CalendarSnapshot("ZZZZZZ", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CalendarSnapshot_WithNoAccessCookie_ReturnsUnauthorized()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();

        var result = await controller.CalendarSnapshot("BCDFGH", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    /// <summary>
    /// Returns the same current dates/final-dates shape Home's own calendar carries — the
    /// reconnected client reconciles its local state against this rather than a full page reload.
    /// </summary>
    [Fact]
    public async Task CalendarSnapshot_WithSignedInParticipant_ReturnsCurrentDatesAndFinalDates()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (signInController, signInHttpContext) = CreateController();
        await signInController.SignIn("BCDFGH", new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4" }, CancellationToken.None);
        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, "WhenWorksWeb.EventAccess.BCDFGH")!;
        var alice = await Db.Participants.SingleAsync(p => p.DisplayName == "Alice");

        var pickedDate = new EventDateBuilder().ForEvent(evt).WithDate(new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero)).Build();
        Db.EventDates.Add(pickedDate);
        await Db.SaveChangesAsync();
        Db.ParticipantAvailabilities.Add(new ParticipantAvailability { ParticipantId = alice.Id, EventDateId = pickedDate.Id });
        Db.EventFinalDates.Add(new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 9, 1) });
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController(requestCookies: new Dictionary<string, string> { ["WhenWorksWeb.EventAccess.BCDFGH"] = cookieValue });

        var result = await controller.CalendarSnapshot("BCDFGH", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var value = json.Value!;
        var dates = (IReadOnlyList<EventCalendarDateViewModel>)value.GetType().GetProperty("dates")!.GetValue(value)!;
        var finalDates = (IReadOnlyList<EventFinalDateViewModel>)value.GetType().GetProperty("finalDates")!.GetValue(value)!;

        var onlyDate = Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 8, 28), onlyDate.Date);
        Assert.Equal([alice.Id], onlyDate.ParticipantIds);

        var onlyFinalDate = Assert.Single(finalDates);
        Assert.Equal(new DateOnly(2026, 9, 1), onlyFinalDate.StartDate);
    }
}
