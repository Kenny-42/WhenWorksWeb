using Microsoft.AspNetCore.Mvc;
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
/// Tier 2 tests for <see cref="WhenWorksWeb.Controllers.EventsController"/>'s ToggleAvailability
/// action (<c>EventsController.Availability.cs</c>) — the Availability tab calendar's find-or-
/// create-on-click toggle endpoint, including the empty-EventDate cleanup and unique-index race
/// handling it's responsible for.
/// </summary>
public class EventsControllerAvailabilityTests : EventsControllerTestFixture
{
    private async Task<Event> CreateEventAsync(string code = "BCDFGH")
    {
        var evt = new EventBuilder().WithCode(code).Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        return evt;
    }

    /// <summary>
    /// Signs a new participant into an already-created event via a real SignIn round trip
    /// (mirroring EventsControllerHomeTests), returning a controller instance already carrying
    /// that participant's real access cookie so ToggleAvailability sees them as the current
    /// participant.
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

    private static int[] GetParticipantIds(JsonResult jsonResult)
    {
        var value = jsonResult.Value!;
        var raw = (System.Collections.IEnumerable)value.GetType().GetProperty("participantIds")!.GetValue(value)!;
        return raw.Cast<int>().ToArray();
    }

    private static string GetDate(JsonResult jsonResult)
    {
        var value = jsonResult.Value!;
        return (string)value.GetType().GetProperty("date")!.GetValue(value)!;
    }

    [Fact]
    public async Task ToggleAvailability_WithNonExistentEventCode_ReturnsNotFound()
    {
        var (controller, _) = CreateController();

        var result = await controller.ToggleAvailability("ZZZZZZ", "2026-08-28", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ToggleAvailability_WithNoAccessCookie_ReturnsUnauthorized()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();

        var result = await controller.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(Db.EventDates);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("")]
    [InlineData("2026/08/28")]
    [InlineData("08-28-2026")]
    [InlineData("2026-8-28")]
    [InlineData("2026-02-30")]
    public async Task ToggleAvailability_WithMalformedDate_ReturnsBadRequest(string malformedDate)
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await controller.ToggleAvailability("BCDFGH", malformedDate, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Empty(Db.EventDates);
    }

    [Fact]
    public async Task ToggleAvailability_WithDateMoreThan50YearsInFuture_ReturnsBadRequest()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();
        var tooFar = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddYears(51).ToString("yyyy-MM-dd");

        var result = await controller.ToggleAvailability("BCDFGH", tooFar, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Empty(Db.EventDates);
    }

    [Fact]
    public async Task ToggleAvailability_WithDateMoreThan50YearsInPast_ReturnsBadRequest()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();
        var tooFar = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddYears(-51).ToString("yyyy-MM-dd");

        var result = await controller.ToggleAvailability("BCDFGH", tooFar, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Empty(Db.EventDates);
    }

    /// <summary>
    /// The bound is inclusive — exactly 50 years out is still a legitimate (if unusual) date to mark, not
    /// an off-by-one rejection.
    /// </summary>
    [Fact]
    public async Task ToggleAvailability_WithDateExactly50YearsInFuture_Succeeds()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();
        var boundary = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddYears(50).ToString("yyyy-MM-dd");

        var result = await controller.ToggleAvailability("BCDFGH", boundary, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Single(GetParticipantIds(json));
    }

    [Fact]
    public async Task ToggleAvailability_WithDateExactly50YearsInPast_Succeeds()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();
        var boundary = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddYears(-50).ToString("yyyy-MM-dd");

        var result = await controller.ToggleAvailability("BCDFGH", boundary, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Single(GetParticipantIds(json));
    }

    [Fact]
    public async Task ToggleAvailability_FirstMarkOnNewDate_CreatesEventDateAndAvailabilityMark()
    {
        var (evt, participant, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await controller.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal("2026-08-28", GetDate(json));
        Assert.Equal([participant.Id], GetParticipantIds(json));

        var eventDate = Assert.Single(Db.EventDates.Where(d => d.EventId == evt.Id));
        Assert.Equal(new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero), eventDate.Date);

        var mark = Assert.Single(Db.ParticipantAvailabilities);
        Assert.Equal(participant.Id, mark.ParticipantId);
        Assert.Equal(eventDate.Id, mark.EventDateId);
    }

    [Fact]
    public async Task ToggleAvailability_SecondParticipantSameDate_AddsToExistingEventDateWithoutDuplicating()
    {
        var evt = await CreateEventAsync();
        var (alice, aliceController) = await SignInParticipantAsync(evt, "Alice", "ff66c4");
        await aliceController.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);

        var (bob, bobController) = await SignInParticipantAsync(evt, "Bob", "111111");
        var result = await bobController.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal([alice.Id, bob.Id], GetParticipantIds(json).OrderBy(id => id));

        // Only one EventDate row for the shared date — not one per participant.
        Assert.Single(Db.EventDates.Where(d => d.EventId == evt.Id));
        Assert.Equal(2, Db.ParticipantAvailabilities.Count());
    }

    [Fact]
    public async Task ToggleAvailability_TogglingOffNonLastMark_RemovesOnlyThatMarkAndKeepsEventDate()
    {
        var evt = await CreateEventAsync();
        var (alice, aliceController) = await SignInParticipantAsync(evt, "Alice", "ff66c4");
        await aliceController.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);

        var (bob, bobController) = await SignInParticipantAsync(evt, "Bob", "111111");
        await bobController.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);

        // Alice un-marks; Bob's mark and the EventDate itself should survive.
        var result = await aliceController.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal([bob.Id], GetParticipantIds(json));

        var eventDate = Assert.Single(Db.EventDates.Where(d => d.EventId == evt.Id));
        var remainingMark = Assert.Single(Db.ParticipantAvailabilities);
        Assert.Equal(bob.Id, remainingMark.ParticipantId);
        Assert.Equal(eventDate.Id, remainingMark.EventDateId);
    }

    /// <summary>
    /// The core cleanup behavior this round of work was built for: once the last participant available
    /// on a date un-marks it, the EventDate row itself must be deleted too, not left behind empty.
    /// </summary>
    [Fact]
    public async Task ToggleAvailability_TogglingOffLastMark_DeletesEventDateEntirely()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        await controller.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);
        Assert.Single(Db.EventDates);

        var result = await controller.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Empty(GetParticipantIds(json));
        Assert.Empty(Db.EventDates.Where(d => d.EventId == evt.Id));
        Assert.Empty(Db.ParticipantAvailabilities);
    }

    [Fact]
    public async Task ToggleAvailability_ToggledThreeTimes_EndsUpMarkedAgain()
    {
        var (_, participant, controller) = await CreateEventWithSignedInParticipantAsync();

        await controller.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None); // on
        await controller.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None); // off (EventDate deleted)
        var result = await controller.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None); // on again

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal([participant.Id], GetParticipantIds(json));
        Assert.Single(Db.EventDates);
    }

    [Fact]
    public async Task ToggleAvailability_OnLeapDay_CreatesEventDateCorrectly()
    {
        var (_, participant, controller) = await CreateEventWithSignedInParticipantAsync();

        // The next Feb 29 from today, computed at runtime so this test doesn't rot as "today" moves on —
        // must also stay inside the 50-year server-enforced bound.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var leapYear = today.Year;
        while (!DateTime.IsLeapYear(leapYear) || new DateOnly(leapYear, 2, 29) <= today)
        {
            leapYear++;
        }
        var leapDay = new DateOnly(leapYear, 2, 29);

        var result = await controller.ToggleAvailability("BCDFGH", leapDay.ToString("yyyy-MM-dd"), CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal([participant.Id], GetParticipantIds(json));
        var eventDate = Assert.Single(Db.EventDates);
        Assert.Equal(new DateTimeOffset(leapYear, 2, 29, 0, 0, 0, TimeSpan.Zero), eventDate.Date);
    }

    [Fact]
    public async Task ToggleAvailability_AtYearBoundary_UsesExactRequestedDateWithNoOffByOne()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        await controller.ToggleAvailability("BCDFGH", "2026-12-31", CancellationToken.None);
        await controller.ToggleAvailability("BCDFGH", "2027-01-01", CancellationToken.None);

        // SQLite can't translate ORDER BY on a DateTimeOffset column, so sort client-side —
        // matches the same limitation already documented on the pre-existing MyEventsController
        // LastActiveAt ordering.
        var dates = Db.EventDates.Select(d => d.Date).ToList().OrderBy(d => d).ToList();
        Assert.Equal(
            [new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)],
            dates);
    }

    [Fact]
    public async Task ToggleAvailability_IsIsolatedPerEvent()
    {
        var (eventA, _, controllerA) = await CreateEventWithSignedInParticipantAsync(code: "BCDFGH");
        var (eventB, _, controllerB) = await CreateEventWithSignedInParticipantAsync(code: "BCDFGJ", displayName: "Alice", color: "ff66c4");

        await controllerA.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);
        await controllerB.ToggleAvailability("BCDFGJ", "2026-08-28", CancellationToken.None);

        Assert.Single(Db.EventDates.Where(d => d.EventId == eventA.Id));
        Assert.Single(Db.EventDates.Where(d => d.EventId == eventB.Id));
        Assert.Equal(2, Db.EventDates.Count());
    }

    /// <summary>
    /// Two participants marking the same never-before-picked date "at the same instant": the interceptor
    /// commits a competing EventDate via a second DbContext at the exact moment this request's own insert
    /// would reach the database, reproducing the unique-index race deterministically (see
    /// EventsControllerSignInTests for the same pattern applied to display-name/color uniqueness). Both
    /// participants' marks must land on the single resulting EventDate row — not a 500, not a duplicate.
    /// </summary>
    [Fact]
    public async Task ToggleAvailability_LosesRaceToConcurrentNewEventDate_RetriesAndSucceeds()
    {
        var (evt, alice, controller) = await CreateEventWithSignedInParticipantAsync(displayName: "Alice", color: "ff66c4");

        var bob = new ParticipantBuilder().ForEvent(evt).WithDisplayName("Bob").WithColor("111111").Build();
        Db.Participants.Add(bob);
        await Db.SaveChangesAsync();

        var utcDate = new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);

        RaceInterceptor.ArmOnce(async () =>
        {
            using var racer = CreateConcurrentDbContext();
            var racerEventDate = new EventDate { EventId = evt.Id, Date = utcDate };
            racerEventDate.Availabilities.Add(new ParticipantAvailability { ParticipantId = bob.Id, EventDateId = 0 });
            racer.EventDates.Add(racerEventDate);
            await racer.SaveChangesAsync();
        });

        var result = await controller.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal([alice.Id, bob.Id], GetParticipantIds(json).OrderBy(id => id));

        // Exactly one EventDate for that day — the race must not have produced a duplicate.
        Assert.Single(Db.EventDates.Where(d => d.EventId == evt.Id && d.Date == utcDate));
    }

    // ---- Live-sync broadcast (see Hubs/EventHub.cs, EventsController.Availability.cs) ----

    [Fact]
    public async Task ToggleAvailability_BroadcastsAvailabilityChangedToEventGroupExcludingCallerConnection()
    {
        var (_, participant, controller) = await CreateEventWithSignedInParticipantAsync();

        await controller.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None, connectionId: "conn-1");

        HubClients.Received(1).GroupExcept(
            EventHub.GroupName("BCDFGH"),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == "conn-1"));

        var broadcast = GetLastBroadcast(HubClientProxy);
        Assert.NotNull(broadcast);
        Assert.Equal("AvailabilityChanged", broadcast!.Value.Method);
        Assert.Equal("2026-08-28", GetPayloadProperty<string>(broadcast.Value.Payload, "date"));
        var participantIds = GetPayloadProperty<IReadOnlyList<int>>(broadcast.Value.Payload, "participantIds");
        Assert.Equal([participant.Id], participantIds);
    }

    /// <summary>
    /// No <c>connectionId</c> given (the client's live-sync connection hasn't started yet) — the
    /// broadcast still goes out, just with nothing excluded, rather than being skipped.
    /// </summary>
    [Fact]
    public async Task ToggleAvailability_WithNoConnectionId_BroadcastsWithEmptyExclusionList()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        await controller.ToggleAvailability("BCDFGH", "2026-08-28", CancellationToken.None);

        HubClients.Received(1).GroupExcept(EventHub.GroupName("BCDFGH"), Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 0));
        Assert.NotNull(GetLastBroadcast(HubClientProxy));
    }
}
