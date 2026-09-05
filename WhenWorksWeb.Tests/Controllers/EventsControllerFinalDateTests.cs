using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using WhenWorksWeb.Controllers;
using WhenWorksWeb.Hubs;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;
using static WhenWorksWeb.Tests.Fixtures.HubBroadcastTestHelper;

namespace WhenWorksWeb.Tests.Controllers;

/// <summary>
/// Tests for <see cref="WhenWorksWeb.Controllers.EventsController"/>'s AddFinalDate/RemoveFinalDate
/// actions (<c>EventsController.FinalDate.cs</c>) — the Finalize tab's "Call the date" card, kept
/// entirely decoupled from <see cref="EventDate"/>/<see cref="ParticipantAvailability"/> per the
/// feature spec.
/// </summary>
public class EventsControllerFinalDateTests : EventsControllerTestFixture
{
    private async Task<Event> CreateEventAsync(string code = "BCDFGH")
    {
        var evt = new EventBuilder().WithCode(code).Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        return evt;
    }

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

    private async Task<(Event Event, Participant Participant, EventsController Controller)> CreateEventWithSignedInParticipantAsync(
        string code = "BCDFGH", string displayName = "Alice", string color = "ff66c4")
    {
        var evt = await CreateEventAsync(code);
        var (participant, controller) = await SignInParticipantAsync(evt, displayName, color);
        return (evt, participant, controller);
    }

    // ---- AddFinalDate ----

    private static Task<IActionResult> AddFinalDateAsync(EventsController controller, string code, string startDate, string? endDate)
        => controller.AddFinalDate(code, new EventAddFinalDateViewModel { StartDate = startDate, EndDate = endDate }, CancellationToken.None);

    [Fact]
    public async Task AddFinalDate_WithNonExistentEventCode_ReturnsEventNotFoundView()
    {
        var (controller, _) = CreateController();

        var result = await AddFinalDateAsync(controller, "ZZZZZZ", "2026-08-28", null);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Error.cshtml", viewResult.ViewName);
    }

    [Fact]
    public async Task AddFinalDate_WithNoAccessCookie_RedirectsToSignIn()
    {
        await CreateEventAsync();
        var (controller, _) = CreateController();

        var result = await AddFinalDateAsync(controller, "BCDFGH", "2026-08-28", null);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSignIn", redirect.RouteName);
    }

    [Fact]
    public async Task AddFinalDate_WhenNotOrganizerAndAnotherOrganizerExists_ReturnsForbidAndDoesNotAdd()
    {
        var evt = await CreateEventAsync();
        var (organizer, _) = await SignInParticipantAsync(evt, "Organizer", "111111");
        organizer.IsOrganizer = true;
        await Db.SaveChangesAsync();

        var (_, controller) = await SignInParticipantAsync(evt, "Alice", "ff66c4");

        var result = await AddFinalDateAsync(controller, "BCDFGH", "2026-08-28", null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(Db.EventFinalDates);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026/08/28")]
    [InlineData("2026-8-28")]
    public async Task AddFinalDate_WithMalformedStartDate_ReturnsFinalizeViewWithModelErrorAndDoesNotAdd(string malformedDate)
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await AddFinalDateAsync(controller, "BCDFGH", malformedDate, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Finalize", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(Db.EventFinalDates);
    }

    [Fact]
    public async Task AddFinalDate_WithEmptyStartDate_ReturnsFinalizeViewWithModelErrorAndDoesNotAdd()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await AddFinalDateAsync(controller, "BCDFGH", "", null);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Finalize", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(Db.EventFinalDates);
    }

    [Fact]
    public async Task AddFinalDate_WithMalformedEndDate_ReturnsFinalizeViewWithModelErrorAndDoesNotAdd()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await AddFinalDateAsync(controller, "BCDFGH", "2026-08-28", "not-a-date");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Finalize", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(Db.EventFinalDates);
    }

    [Theory]
    [InlineData("1970-01-01")]
    [InlineData("2200-01-01")]
    public async Task AddFinalDate_WithStartDateOutsideFiftyYearBound_ReturnsFinalizeViewWithModelErrorAndDoesNotAdd(string outOfBoundDate)
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await AddFinalDateAsync(controller, "BCDFGH", outOfBoundDate, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Finalize", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(Db.EventFinalDates);
    }

    [Fact]
    public async Task AddFinalDate_WithEndDateOutsideFiftyYearBound_ReturnsFinalizeViewWithModelErrorAndDoesNotAdd()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await AddFinalDateAsync(controller, "BCDFGH", "2026-08-28", "2200-01-01");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Finalize", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(Db.EventFinalDates);
    }

    [Fact]
    public async Task AddFinalDate_WhenEventAlreadyHasMaximumFinalDates_ReturnsFinalizeViewWithModelErrorAndDoesNotAdd()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        for (var i = 0; i < WhenWorksWeb.Common.ModelConstants.EventFinalDateMaxCount; i++)
        {
            Db.EventFinalDates.Add(new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 1, 1).AddDays(i) });
        }
        await Db.SaveChangesAsync();
        var knownIds = string.Join(",", Db.EventFinalDates.Where(f => f.EventId == evt.Id).Select(f => f.Id));

        var result = await controller.AddFinalDate(
            "BCDFGH",
            new EventAddFinalDateViewModel { StartDate = "2026-08-28", KnownFinalDateIds = knownIds },
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Finalize", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(WhenWorksWeb.Common.ModelConstants.EventFinalDateMaxCount, Db.EventFinalDates.Count(f => f.EventId == evt.Id));
    }

    [Fact]
    public async Task AddFinalDate_WithSingleDay_CreatesRowWithNullEndDateAndRedirects()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await AddFinalDateAsync(controller, "BCDFGH", "2026-08-28", null);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventFinalize", redirect.RouteName);

        var finalDate = Assert.Single(Db.EventFinalDates.Where(f => f.EventId == evt.Id));
        Assert.Equal(new DateOnly(2026, 8, 28), finalDate.StartDate);
        Assert.Null(finalDate.EndDate);
    }

    [Fact]
    public async Task AddFinalDate_WithValidRange_CreatesRowWithBothDates()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        await AddFinalDateAsync(controller, "BCDFGH", "2026-09-03", "2026-09-05");

        var finalDate = Assert.Single(Db.EventFinalDates.Where(f => f.EventId == evt.Id));
        Assert.Equal(new DateOnly(2026, 9, 3), finalDate.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 5), finalDate.EndDate);
    }

    /// <summary>The bound is inclusive — a single-day "range" (start == end) is valid, not rejected.</summary>
    [Fact]
    public async Task AddFinalDate_WithEndDateEqualToStartDate_Succeeds()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        await AddFinalDateAsync(controller, "BCDFGH", "2026-08-28", "2026-08-28");

        var finalDate = Assert.Single(Db.EventFinalDates.Where(f => f.EventId == evt.Id));
        Assert.Equal(new DateOnly(2026, 8, 28), finalDate.EndDate);
    }

    [Fact]
    public async Task AddFinalDate_WithEndDateBeforeStartDate_ReturnsFinalizeViewWithModelErrorAndDoesNotAdd()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await AddFinalDateAsync(controller, "BCDFGH", "2026-08-28", "2026-08-20");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Finalize", viewResult.ViewName);
        Assert.IsType<EventFinalizeViewModel>(viewResult.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(Db.EventFinalDates.Where(f => f.EventId == evt.Id));
    }

    [Fact]
    public async Task AddFinalDate_CanBeCalledRepeatedlyForNonConsecutiveDates()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        await AddFinalDateAsync(controller, "BCDFGH", "2026-08-20", null);
        var firstId = Db.EventFinalDates.Single(f => f.EventId == evt.Id).Id;
        await controller.AddFinalDate(
            "BCDFGH",
            new EventAddFinalDateViewModel { StartDate = "2026-09-03", EndDate = "2026-09-05", KnownFinalDateIds = firstId.ToString() },
            CancellationToken.None);

        Assert.Equal(2, Db.EventFinalDates.Count(f => f.EventId == evt.Id));
    }

    // ---- RemoveFinalDate ----

    [Fact]
    public async Task RemoveFinalDate_WithNonExistentEventCode_ReturnsEventNotFoundView()
    {
        var (controller, _) = CreateController();

        var result = await controller.RemoveFinalDate("ZZZZZZ", 1, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Error.cshtml", viewResult.ViewName);
    }

    [Fact]
    public async Task RemoveFinalDate_WithNoAccessCookie_RedirectsToSignIn()
    {
        await CreateEventAsync();
        var (controller, _) = CreateController();

        var result = await controller.RemoveFinalDate("BCDFGH", 1, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSignIn", redirect.RouteName);
    }

    [Fact]
    public async Task RemoveFinalDate_WhenNotOrganizerAndAnotherOrganizerExists_ReturnsForbidAndDoesNotRemove()
    {
        var evt = await CreateEventAsync();
        var finalDate = new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 8, 28) };
        Db.EventFinalDates.Add(finalDate);
        var (organizer, _) = await SignInParticipantAsync(evt, "Organizer", "111111");
        organizer.IsOrganizer = true;
        await Db.SaveChangesAsync();

        var (_, controller) = await SignInParticipantAsync(evt, "Alice", "ff66c4");

        var result = await controller.RemoveFinalDate("BCDFGH", finalDate.Id, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Single(Db.EventFinalDates);
    }

    [Fact]
    public async Task RemoveFinalDate_WithValidId_RemovesRowAndRedirects()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        var finalDate = new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 8, 28) };
        Db.EventFinalDates.Add(finalDate);
        await Db.SaveChangesAsync();

        var result = await controller.RemoveFinalDate("BCDFGH", finalDate.Id, CancellationToken.None, knownFinalDateIds: finalDate.Id.ToString());

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventFinalize", redirect.RouteName);
        Assert.Empty(Db.EventFinalDates.Where(f => f.EventId == evt.Id));
    }

    [Fact]
    public async Task RemoveFinalDate_WithNonExistentId_RedirectsWithoutError()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await controller.RemoveFinalDate("BCDFGH", 999999, CancellationToken.None);

        Assert.IsType<RedirectToRouteResult>(result);
    }

    /// <summary>
    /// A final date id that belongs to a different event must not be removable via this event's
    /// code — the query scopes by EventId, not just the id, so guessing another event's row id
    /// can't delete it.
    /// </summary>
    [Fact]
    public async Task RemoveFinalDate_WithIdBelongingToAnotherEvent_DoesNotRemoveIt()
    {
        var otherEvent = await CreateEventAsync("BCDFGJ");
        var otherEventFinalDate = new EventFinalDate { EventId = otherEvent.Id, StartDate = new DateOnly(2026, 8, 28) };
        Db.EventFinalDates.Add(otherEventFinalDate);
        await Db.SaveChangesAsync();

        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync(code: "BCDFGH");

        var result = await controller.RemoveFinalDate("BCDFGH", otherEventFinalDate.Id, CancellationToken.None);

        Assert.IsType<RedirectToRouteResult>(result);
        Assert.Single(Db.EventFinalDates);
        Assert.NotNull(await Db.EventFinalDates.FindAsync(otherEventFinalDate.Id));
    }

    // ---- Final-dates concurrency check (see FinalDatesAreStaleAsync in EventsController.FinalDate.cs) ----

    [Fact]
    public async Task AddFinalDate_WithStaleKnownFinalDateIds_ReturnsFinalizeViewWithModelErrorAndDoesNotAdd()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        Db.EventFinalDates.Add(new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 1, 1) });
        await Db.SaveChangesAsync();

        // KnownFinalDateIds left null/empty — doesn't match the event's actual one final date.
        var result = await controller.AddFinalDate(
            "BCDFGH", new EventAddFinalDateViewModel { StartDate = "2026-08-28" }, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Finalize", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Single(Db.EventFinalDates.Where(f => f.EventId == evt.Id));
        HubClients.DidNotReceiveWithAnyArgs().Group(default!);
    }

    [Fact]
    public async Task AddFinalDate_WithCurrentKnownFinalDateIds_Succeeds()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        var existing = new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 1, 1) };
        Db.EventFinalDates.Add(existing);
        await Db.SaveChangesAsync();

        var result = await controller.AddFinalDate(
            "BCDFGH",
            new EventAddFinalDateViewModel { StartDate = "2026-08-28", KnownFinalDateIds = existing.Id.ToString() },
            CancellationToken.None);

        Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal(2, Db.EventFinalDates.Count(f => f.EventId == evt.Id));
    }

    [Fact]
    public async Task RemoveFinalDate_WithStaleKnownFinalDateIds_ReturnsFinalizeViewWithModelErrorAndDoesNotRemove()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        var finalDate = new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 8, 28) };
        Db.EventFinalDates.Add(finalDate);
        await Db.SaveChangesAsync();

        // A second final date was added server-side after this client's knownFinalDateIds (just
        // finalDate.Id) was rendered — the set no longer matches.
        var otherFinalDate = new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 9, 1) };
        Db.EventFinalDates.Add(otherFinalDate);
        await Db.SaveChangesAsync();

        var result = await controller.RemoveFinalDate("BCDFGH", finalDate.Id, CancellationToken.None, knownFinalDateIds: finalDate.Id.ToString());

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Finalize", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(2, Db.EventFinalDates.Count(f => f.EventId == evt.Id));
    }

    // ---- Live-sync broadcast (see Hubs/EventHub.cs) ----

    [Fact]
    public async Task AddFinalDate_BroadcastsFinalDatesChangedToEventGroup()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        await AddFinalDateAsync(controller, "BCDFGH", "2026-08-28", null);

        HubClients.Received(1).Group(EventHub.GroupName("BCDFGH"));

        var broadcast = GetLastBroadcast(HubClientProxy);
        Assert.NotNull(broadcast);
        Assert.Equal("FinalDatesChanged", broadcast!.Value.Method);
        var finalDates = GetPayloadProperty<IReadOnlyList<EventFinalDateViewModel>>(broadcast.Value.Payload, "finalDates");
        var finalDate = Assert.Single(finalDates);
        Assert.Equal(new DateOnly(2026, 8, 28), finalDate.StartDate);
    }

    [Fact]
    public async Task RemoveFinalDate_BroadcastsFinalDatesChangedToEventGroup()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        var finalDate = new EventFinalDate { EventId = evt.Id, StartDate = new DateOnly(2026, 8, 28) };
        Db.EventFinalDates.Add(finalDate);
        await Db.SaveChangesAsync();

        await controller.RemoveFinalDate("BCDFGH", finalDate.Id, CancellationToken.None, knownFinalDateIds: finalDate.Id.ToString());

        HubClients.Received(1).Group(EventHub.GroupName("BCDFGH"));

        var broadcast = GetLastBroadcast(HubClientProxy);
        Assert.NotNull(broadcast);
        Assert.Equal("FinalDatesChanged", broadcast!.Value.Method);
        var finalDates = GetPayloadProperty<IReadOnlyList<EventFinalDateViewModel>>(broadcast.Value.Payload, "finalDates");
        Assert.Empty(finalDates);
    }

    [Fact]
    public async Task RemoveFinalDate_WithNonExistentId_DoesNotBroadcast()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        await controller.RemoveFinalDate("BCDFGH", 999999, CancellationToken.None);

        HubClients.DidNotReceiveWithAnyArgs().Group(default!);
    }
}
