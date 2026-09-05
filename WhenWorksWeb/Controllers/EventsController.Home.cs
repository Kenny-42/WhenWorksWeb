using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// How many months before the current month the Availability tab's calendar can page back
    /// to, and how many months after it can page forward to. This is a sanity bound, not a data-
    /// volume optimization — the JSON payload's size is driven entirely by how many candidate
    /// dates actually have picks (see <see cref="EventCalendarViewModel"/>), not by how wide this
    /// window is, so it's set generously (10 years either way) rather than tightly.
    /// </summary>
    private const int CalendarMonthsBeforeToday = 120;

    /// <summary>How many months after the current month the calendar can page forward to.</summary>
    private const int CalendarMonthsAfterToday = 120;

    /// <summary>
    /// Displays the event home page for the event associated with the specified code.
    /// </summary>
    /// <remarks>The page is only accessible after a successful sign-in flow has issued a valid access cookie.</remarks>
    [HttpGet("/event/{code}", Name = "EventHome")]
    public async Task<IActionResult> Home(string code, CancellationToken cancellationToken)
    {
        var eventEntity = await GetEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return CreateEventNotFoundResult();
        }

        var participant = await GetCurrentParticipantAsync(eventEntity, currentUser: null, includeUserFallback: false, cancellationToken);
        if (participant is null)
        {
            return RedirectToRoute("EventSignIn", new { code = eventEntity.Code });
        }

        var emoji = await GetEventEmojiAsync(eventEntity, cancellationToken);
        var description = await GetEventDescriptionAsync(eventEntity, cancellationToken);
        var calendar = await BuildEventCalendarAsync(eventEntity, participant, cancellationToken);
        var canManageEvent = await CanManageEventAsync(eventEntity, participant, cancellationToken);

        return View(new EventHomeViewModel
        {
            Header = BuildEventHeader(eventEntity, emoji, EventTab.Availability, description),
            Calendar = calendar,
            CanManageEvent = canManageEvent
        });
    }

    /// <summary>
    /// Builds the calendar data for the Availability tab: every participant (for the legend), and
    /// every candidate date that has at least one participant marked available on it (sparse —
    /// dates with no picks aren't included, since the client generates the whole grid from
    /// calendar math and only needs to know which cells have picks).
    /// </summary>
    private async Task<EventCalendarViewModel> BuildEventCalendarAsync(Event eventEntity, Participant currentParticipant, CancellationToken cancellationToken)
    {
        var participants = await _db.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventEntity.Id)
            .OrderBy(p => p.DisplayName)
            .Select(p => new EventCalendarParticipantViewModel
            {
                Id = p.Id,
                DisplayName = p.DisplayName,
                Color = p.Color
            })
            .ToListAsync(cancellationToken);

        var (dates, finalDates) = await BuildEventCalendarDatesAsync(eventEntity, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var initialMonth = new DateOnly(today.Year, today.Month, 1);

        return new EventCalendarViewModel
        {
            InitialMonth = initialMonth,
            WindowStartMonth = initialMonth.AddMonths(-CalendarMonthsBeforeToday),
            WindowEndMonth = initialMonth.AddMonths(CalendarMonthsAfterToday),
            CurrentParticipantId = currentParticipant.Id,
            Participants = participants,
            Dates = dates,
            FinalDates = finalDates
        };
    }

    /// <summary>
    /// Queries just the candidate dates (with their availability marks) and final dates for an
    /// event — the live-sync-able slice of <see cref="BuildEventCalendarAsync"/>'s data, factored
    /// out so both it and <see cref="CalendarSnapshot"/> (the reconnect catch-up endpoint) share
    /// one query rather than two copies that could drift apart.
    /// </summary>
    private async Task<(IReadOnlyList<EventCalendarDateViewModel> Dates, IReadOnlyList<EventFinalDateViewModel> FinalDates)> BuildEventCalendarDatesAsync(
        Event eventEntity, CancellationToken cancellationToken)
    {
        // Two round trips (dates, then their availability marks) rather than a nested collection
        // projection, so this stays portable to the SQLite provider the test suite runs against.
        var eventDates = await _db.EventDates
            .AsNoTracking()
            .Where(d => d.EventId == eventEntity.Id)
            .Select(d => new { d.Id, d.Date })
            .ToListAsync(cancellationToken);

        var availabilitiesByEventDateId = await _db.ParticipantAvailabilities
            .AsNoTracking()
            .Where(a => a.EventDate.EventId == eventEntity.Id)
            .Select(a => new { a.EventDateId, a.ParticipantId })
            .ToListAsync(cancellationToken);

        var participantIdsByEventDateId = availabilitiesByEventDateId
            .GroupBy(a => a.EventDateId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(a => a.ParticipantId).ToList());

        var dates = eventDates
            .Where(d => participantIdsByEventDateId.ContainsKey(d.Id))
            .Select(d => new EventCalendarDateViewModel
            {
                Date = DateOnly.FromDateTime(d.Date.UtcDateTime),
                ParticipantIds = participantIdsByEventDateId[d.Id]
            })
            .ToList();

        var finalDates = await _db.EventFinalDates
            .AsNoTracking()
            .Where(f => f.EventId == eventEntity.Id)
            .OrderBy(f => f.StartDate)
            .Select(f => new EventFinalDateViewModel
            {
                Id = f.Id,
                StartDate = f.StartDate,
                EndDate = f.EndDate
            })
            .ToListAsync(cancellationToken);

        return (dates, finalDates);
    }

    /// <summary>
    /// Reconnect catch-up snapshot: the current candidate dates + final dates, for the live-sync
    /// connection (see <c>wwwroot/js/event-live-sync.js</c>) to reconcile against after a dropped
    /// connection auto-reconnects, in case a broadcast was missed while it was down. Deliberately
    /// scoped to just this calendar/final-dates slice (not the full <see cref="EventHomeViewModel"/>)
    /// — see the feature spec's Design Decisions section.
    /// </summary>
    [HttpGet("/event/{code}/calendar-snapshot", Name = "EventCalendarSnapshot")]
    public async Task<IActionResult> CalendarSnapshot(string code, CancellationToken cancellationToken)
    {
        var eventEntity = await GetEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return NotFound();
        }

        var participant = await GetCurrentParticipantAsync(eventEntity, currentUser: null, includeUserFallback: false, cancellationToken);
        if (participant is null)
        {
            return Unauthorized();
        }

        var (dates, finalDates) = await BuildEventCalendarDatesAsync(eventEntity, cancellationToken);

        return Json(new { dates, finalDates });
    }
}
