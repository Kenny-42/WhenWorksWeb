using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Controllers;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Controllers;

/// <summary>
/// Tests for <see cref="WhenWorksWeb.Controllers.EventsController"/>'s Finalize tab GET action
/// (<c>EventsController.Finalize.cs</c>): the "Call the date" suggestions/add-date form and the
/// "Final dates" list, moved off the Settings tab.
/// </summary>
public class EventsControllerFinalizeTests : EventsControllerTestFixture
{
    private async Task<Event> CreateEventAsync(string code = "BCDFGH")
    {
        var evt = new EventBuilder().WithCode(code).Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        return evt;
    }

    /// <summary>
    /// Signs a new participant into an already-created event via a real SignIn round trip,
    /// returning a controller instance already carrying that participant's real access cookie.
    /// The event's creator cookie is never set by this helper, so — per the Organizer Permission
    /// Model — this participant is not auto-flagged IsOrganizer; tests that need an organizer set
    /// <see cref="Participant.IsOrganizer"/> on the returned participant explicitly.
    /// </summary>
    private async Task<(Participant Participant, EventsController Controller)> SignInParticipantAsync(
        Event evt, string displayName = "Alice", string color = "ff66c4")
    {
        var code = evt.Code;

        var (signInController, signInHttpContext) = CreateController();
        await signInController.SignIn(code, new EventSignInViewModel { Code = code, DisplayName = displayName, Color = color }, CancellationToken.None);

        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, $"WhenWorksWeb.EventAccess.{code}");
        Assert.NotNull(cookieValue);

        var (controller, _) = CreateController(
            requestCookies: new Dictionary<string, string> { [$"WhenWorksWeb.EventAccess.{code}"] = cookieValue! });

        var participant = await Db.Participants.SingleAsync(p => p.EventId == evt.Id && p.DisplayName == displayName);

        return (participant, controller);
    }

    /// <summary>Convenience wrapper for the common case: one event, one participant.</summary>
    private async Task<(Event Event, Participant Participant, EventsController Controller)> CreateEventWithSignedInParticipantAsync(
        string code = "BCDFGH", string displayName = "Alice", string color = "ff66c4")
    {
        var evt = await CreateEventAsync(code);
        var (participant, controller) = await SignInParticipantAsync(evt, displayName, color);
        return (evt, participant, controller);
    }

    [Fact]
    public async Task Finalize_WithNonExistentEventCode_ReturnsEventNotFoundView()
    {
        var (controller, _) = CreateController();

        var result = await controller.Finalize("ZZZZZZ", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Error.cshtml", viewResult.ViewName);
    }

    [Fact]
    public async Task Finalize_WithNoAccessCookie_RedirectsToSignIn()
    {
        await CreateEventAsync();
        var (controller, _) = CreateController();

        var result = await controller.Finalize("BCDFGH", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSignIn", redirect.RouteName);
    }

    [Fact]
    public async Task Finalize_WithSoleParticipantAndNoOrganizer_FallsOpenAndCanManageEventIsTrue()
    {
        // The event has zero IsOrganizer participants — the fallback-open rule in
        // CanManageEventAsync should let this lone participant manage it.
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await controller.Finalize("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventFinalizeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.CanManageEvent);
    }

    [Fact]
    public async Task Finalize_WithAnotherOrganizerPresent_NonOrganizerCanManageEventIsFalse()
    {
        var evt = await CreateEventAsync();
        var (organizer, _) = await SignInParticipantAsync(evt, "Organizer", "111111");
        organizer.IsOrganizer = true;
        await Db.SaveChangesAsync();

        var (_, controller) = await SignInParticipantAsync(evt, "Alice", "ff66c4");

        var result = await controller.Finalize("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventFinalizeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.CanManageEvent);
    }

    [Fact]
    public async Task Finalize_ReturnsCurrentFinalDates()
    {
        var evt = await CreateEventAsync();
        Db.EventFinalDates.Add(new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 8, 28) });
        await Db.SaveChangesAsync();

        var (_, controller) = await SignInParticipantAsync(evt);

        var result = await controller.Finalize("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventFinalizeViewModel>(Assert.IsType<ViewResult>(result).Model);
        var finalDate = Assert.Single(model.FinalDates);
        Assert.Equal(new DateOnly(2026, 8, 28), finalDate.StartDate);
        Assert.Null(finalDate.EndDate);
    }

    [Fact]
    public async Task Finalize_WithNoFinalDates_ReturnsEmptyFinalDatesList()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await controller.Finalize("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventFinalizeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.FinalDates);
    }

    [Fact]
    public async Task Finalize_ReturnsSameCalendarDataAsAvailabilityTab()
    {
        var (evt, participant, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await controller.Finalize("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventFinalizeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(participant.Id, model.Calendar.CurrentParticipantId);
    }

    [Fact]
    public async Task Finalize_SetsActiveTabToFinalize()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await controller.Finalize("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventFinalizeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(EventTab.Finalize, model.Header.ActiveTab);
    }
}
