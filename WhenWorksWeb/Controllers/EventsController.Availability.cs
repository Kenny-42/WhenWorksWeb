using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Hubs;
using WhenWorksWeb.Models;
using WhenWorksWeb.Services;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// Toggles the current participant's own availability on a single calendar date: finds or
    /// creates the <see cref="EventDate"/> row for that day on the fly (no separate "propose a
    /// date" step — this was a deliberate design decision, see the feature spec's Schema Changes
    /// section), then adds or removes their <see cref="ParticipantAvailability"/> row. If that
    /// removal leaves the date with zero participants available, the now-empty
    /// <see cref="EventDate"/> row is deleted too, rather than left behind as dead data.
    /// </summary>
    /// <remarks>
    /// Posted as a plain form field (not JSON) so the standard antiforgery-token validation below
    /// works unchanged from every other POST in this controller — the calendar's JS reads the
    /// page's existing <c>@Html.AntiForgeryToken()</c> field and sends it the same way a normal
    /// form submit would, just via fetch instead of a full-page navigation.
    ///
    /// <paramref name="connectionId"/> is declared last (after <paramref name="cancellationToken"/>,
    /// unlike the usual "CancellationToken last" convention) purely so it can default to null
    /// without disturbing every existing positional call site's argument order — it's the caller's
    /// own live-sync SignalR connection id (see <c>wwwroot/js/event-live-sync.js</c>), used only to
    /// exclude that connection from the broadcast below (its own tab already applied the change
    /// from this action's direct JSON response).
    /// </remarks>
    [HttpPost("/event/{code}/availability", Name = "EventToggleAvailability")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAvailability(string code, [FromForm] string date, CancellationToken cancellationToken, [FromForm] string? connectionId = null)
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

        // The calendar only ever sends dates it generated itself from calendar math, so a
        // malformed value here means a bad/tampered request rather than a real user action.
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return BadRequest();
        }

        // The calendar's own UI caps how far a participant can page, but that's a client-side
        // convenience only — this is the actual, server-enforced boundary, since nothing stops a
        // request from being sent directly with an arbitrary date. Shared with AddFinalDate in
        // EventsController.FinalDate.cs via ModelConstants.UserSuppliedDateBoundYears.
        var today = ResolveEventLocalToday(eventEntity);
        if (day < today.AddYears(-ModelConstants.UserSuppliedDateBoundYears) || day > today.AddYears(ModelConstants.UserSuppliedDateBoundYears))
        {
            return BadRequest();
        }

        // Calendar days are always stored as UTC midnight, regardless of the event's TimeZoneId —
        // that id is a display/interpretation lens only (which local day a stored value represents,
        // and where "today" above rolls over), not a storage format; EventDate.Date carries no
        // time-of-day meaning anywhere else in the app yet. See the event-timezone feature spec.
        var utcDate = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var participantIds = await ToggleAvailabilityMarkAsync(eventEntity.Id, participant.Id, utcDate, cancellationToken);

        // Same payload shape as the JSON response below, pushed to every other connected viewer
        // of this event — the acting connection is excluded (when known) since its own tab
        // already applied this update from the response it's about to receive.
        IReadOnlyList<string> excludedConnectionIds = connectionId is null ? Array.Empty<string>() : new[] { connectionId };
        await _hub.Clients.GroupExcept(EventHub.GroupName(code), excludedConnectionIds)
            .SendAsync("AvailabilityChanged", new { date, participantIds }, cancellationToken);

        return Json(new { date, participantIds });
    }

    /// <summary>
    /// Adds or removes <paramref name="participantId"/>'s availability mark on
    /// <paramref name="utcDate"/> for <paramref name="eventId"/>. Returns the resulting set of
    /// participant ids available on that date (empty if none remain).
    /// </summary>
    private async Task<IReadOnlyList<int>> ToggleAvailabilityMarkAsync(int eventId, int participantId, DateTimeOffset utcDate, CancellationToken cancellationToken)
    {
        var eventDate = await _db.EventDates
            .SingleOrDefaultAsync(d => d.EventId == eventId && d.Date == utcDate, cancellationToken);

        int eventDateId;

        if (eventDate is null)
        {
            eventDate = await CreateEventDateWithFirstMarkAsync(eventId, participantId, utcDate, cancellationToken);
            eventDateId = eventDate.Id;
        }
        else
        {
            eventDateId = eventDate.Id;

            var existingMark = await _db.ParticipantAvailabilities
                .SingleOrDefaultAsync(a => a.ParticipantId == participantId && a.EventDateId == eventDateId, cancellationToken);

            if (existingMark is null)
            {
                _db.ParticipantAvailabilities.Add(new ParticipantAvailability { ParticipantId = participantId, EventDateId = eventDateId });
                await _db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                // Removing a mark and (possibly) cleaning up the now-empty EventDate it leaves
                // behind must succeed or fail together — the multi-step write this codebase
                // otherwise wraps in an explicit transaction (see MyEventsController.Delete).
                using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    _db.ParticipantAvailabilities.Remove(existingMark);
                    await _db.SaveChangesAsync(cancellationToken);

                    // The mark just removed may have been the last one on this date — sweep the
                    // whole event's now-empty candidate dates rather than re-deriving just this
                    // one, which also self-heals any rows orphaned before this cleanup existed.
                    await _eventDateCleanup.RemoveEmptyDatesAsync(eventId, cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
        }

        // Whether or not the EventDate row still exists, querying by its id correctly returns an
        // empty list if it (and its marks) were just cleaned up above.
        return await _db.ParticipantAvailabilities
            .AsNoTracking()
            .Where(a => a.EventDateId == eventDateId)
            .Select(a => a.ParticipantId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Creates the <see cref="EventDate"/> row for a day with no candidate-date row yet, along
    /// with its first <see cref="ParticipantAvailability"/> mark, in a single round trip.
    /// </summary>
    /// <remarks>
    /// Retries once: two participants marking the same never-before-picked date at the same
    /// instant can both pass <see cref="ToggleAvailabilityMarkAsync"/>'s "does this EventDate
    /// exist yet?" check before either commits, and the losing insert then violates the unique
    /// index on <c>(EventId, Date)</c>. Rather than surface that as a 500, the failed attempt
    /// re-queries for the row the other request just committed and adds this participant's mark
    /// onto it instead of creating a duplicate.
    /// </remarks>
    private async Task<EventDate> CreateEventDateWithFirstMarkAsync(int eventId, int participantId, DateTimeOffset utcDate, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var eventDate = new EventDate { EventId = eventId, Date = utcDate };
            // Adding the mark through the navigation property rather than setting EventDateId
            // directly lets EF resolve the FK from the relationship once eventDate.Id is
            // assigned, so both rows are written in a single round trip instead of saving the
            // EventDate first just to learn its id.
            eventDate.Availabilities.Add(new ParticipantAvailability { ParticipantId = participantId, EventDateId = 0 });
            _db.EventDates.Add(eventDate);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return eventDate;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // Drop the failed insert from tracking so the retry starts from a clean read
                // instead of carrying over a half-added entity from the losing insert.
                _db.ChangeTracker.Clear();

                var winner = await _db.EventDates
                    .SingleOrDefaultAsync(d => d.EventId == eventId && d.Date == utcDate, cancellationToken);

                if (winner is not null)
                {
                    winner.Availabilities.Add(new ParticipantAvailability { ParticipantId = participantId, EventDateId = winner.Id });
                    await _db.SaveChangesAsync(cancellationToken);
                    return winner;
                }
                // If it still isn't there, the failure wasn't actually a collision on this exact
                // date — fall through and let the loop's next iteration attempt a plain create.
            }
        }

        // Unreachable in practice — a second consecutive failure would mean something is wrong
        // beyond ordinary concurrent-click contention, so let it surface as a 500 instead of
        // looping forever.
        throw new InvalidOperationException($"Failed to create availability for event {eventId} after {maxAttempts} attempts.");
    }

    /// <summary>
    /// Updates the event's <see cref="Event.TimeZoneId"/> — the Availability tab calendar card
    /// header's timezone picker. Organizer-gated the same as the Settings tab's mutations (see
    /// <see cref="AuthorizeEventActionAsync"/>), even though the control itself lives on this tab.
    /// This only relabels which zone <see cref="EventDate.Date"/>'s stored UTC-midnight values are
    /// read in — it never moves any existing candidate date (see the feature spec's Design
    /// Decisions), which is why there's no confirmation/staleness check here beyond the client's own
    /// plain-language confirmation dialog before submit.
    /// </summary>
    [HttpPost("/event/{code}/availability/timezone", Name = "EventUpdateTimeZone")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTimeZone(string code, EventUpdateTimeZoneViewModel model, CancellationToken cancellationToken)
    {
        var (context, failure) = await AuthorizeEventActionAsync(code, CanManageEventAsync, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var (eventEntity, participant) = context!.Value;

        if (!TryValidateModel(model) || !_timeZoneOptionsProvider.IsValidTimeZoneId(model.TimeZoneId))
        {
            ModelState.AddModelError(nameof(EventUpdateTimeZoneViewModel.TimeZoneId), "Choose a valid timezone.");
            var emoji = await GetEventEmojiAsync(eventEntity, cancellationToken);
            var description = await GetEventDescriptionAsync(eventEntity, cancellationToken);
            var calendar = await BuildEventCalendarAsync(eventEntity, participant, cancellationToken);

            return View("Home", new EventHomeViewModel
            {
                Header = BuildEventHeader(eventEntity, emoji, EventTab.Availability, description),
                Calendar = calendar,
                CanManageEvent = true,
                CurrentTimeZoneId = eventEntity.TimeZoneId,
                TimeZoneGroups = _timeZoneOptionsProvider.GetGroupedOptions()
            });
        }

        eventEntity.TimeZoneId = model.TimeZoneId;
        eventEntity.LastActiveAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToRoute("EventHome", new { code = eventEntity.Code });
    }
}
