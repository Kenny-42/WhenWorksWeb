using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Hubs;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// The error shown when an Add/Remove Final Date submission's <c>knownFinalDateIds</c> (the
    /// set the client had at page-load/last-render — see <see cref="FinalDatesAreStaleAsync"/>)
    /// no longer matches the event's current final dates: someone else changed the list after this
    /// client's copy was rendered, so the submit is rejected rather than silently overwriting it.
    /// </summary>
    private const string FinalDatesStaleErrorMessage = "Final dates changed — review and try again.";

    /// <summary>
    /// Adds an organizer-chosen final date (or date range) to the event. Organizer-only, and
    /// entirely independent of <see cref="EventDate"/>/<see cref="ParticipantAvailability"/> — see
    /// the Schema Changes section of the feature spec for why the two are deliberately decoupled.
    /// </summary>
    [HttpPost("/event/{code}/finalize/final-dates", Name = "EventAddFinalDate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFinalDate(string code, EventAddFinalDateViewModel model, CancellationToken cancellationToken)
    {
        var eventEntity = await GetTrackedEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return CreateEventNotFoundResult();
        }

        var participant = await GetCurrentParticipantAsync(eventEntity, currentUser: null, includeUserFallback: false, cancellationToken);
        if (participant is null)
        {
            return RedirectToRoute("EventSignIn", new { code = eventEntity.Code });
        }

        if (!await CanManageEventAsync(eventEntity, participant, cancellationToken))
        {
            return Forbid();
        }

        if (await FinalDatesAreStaleAsync(eventEntity.Id, model.KnownFinalDateIds, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, FinalDatesStaleErrorMessage);
            return View("Finalize", await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
        }

        if (!TryValidateModel(model))
        {
            return View("Finalize", await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var earliestAllowed = today.AddYears(-ModelConstants.UserSuppliedDateBoundYears);
        var latestAllowed = today.AddYears(ModelConstants.UserSuppliedDateBoundYears);

        // A malformed value here used to be a bare BadRequest() — now a friendly, view-rendered
        // ModelState error, same as every other validation failure on this form, since a bad date
        // is just as reachable by a normal user mistyping/mis-pasting as by a tampered request.
        if (!DateOnly.TryParseExact(model.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
        {
            ModelState.AddModelError(nameof(EventAddFinalDateViewModel.StartDate), "Enter a valid start date.");
            return View("Finalize", await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
        }

        if (start < earliestAllowed || start > latestAllowed)
        {
            ModelState.AddModelError(nameof(EventAddFinalDateViewModel.StartDate),
                $"Start date must be within {ModelConstants.UserSuppliedDateBoundYears} years of today.");
            return View("Finalize", await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
        }

        DateOnly? end = null;
        if (!string.IsNullOrWhiteSpace(model.EndDate))
        {
            if (!DateOnly.TryParseExact(model.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEnd))
            {
                ModelState.AddModelError(nameof(EventAddFinalDateViewModel.EndDate), "Enter a valid end date.");
                return View("Finalize", await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
            }

            if (parsedEnd < start)
            {
                ModelState.AddModelError(nameof(EventAddFinalDateViewModel.EndDate), "End date must be on or after the start date.");
                return View("Finalize", await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
            }

            if (parsedEnd > latestAllowed)
            {
                ModelState.AddModelError(nameof(EventAddFinalDateViewModel.EndDate),
                    $"End date must be within {ModelConstants.UserSuppliedDateBoundYears} years of today.");
                return View("Finalize", await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
            }

            end = parsedEnd;
        }

        var existingFinalDateCount = await _db.EventFinalDates.CountAsync(f => f.EventId == eventEntity.Id, cancellationToken);
        if (existingFinalDateCount >= ModelConstants.EventFinalDateMaxCount)
        {
            ModelState.AddModelError(nameof(EventAddFinalDateViewModel.StartDate),
                $"This event already has the maximum of {ModelConstants.EventFinalDateMaxCount} final dates.");
            return View("Finalize", await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
        }

        _db.EventFinalDates.Add(new EventFinalDate
        {
            EventId = eventEntity.Id,
            StartDate = start,
            EndDate = end
        });
        await _db.SaveChangesAsync(cancellationToken);

        await BroadcastFinalDatesChangedAsync(eventEntity, cancellationToken);

        return RedirectToRoute("EventFinalize", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Removes an organizer-chosen final date entry from the event. Organizer-only; scoped to the
    /// requested event so one event's organizer can't remove another event's row by guessing an id.
    /// </summary>
    /// <remarks>
    /// <paramref name="knownFinalDateIds"/> is declared last (after <paramref name="cancellationToken"/>)
    /// purely so it can default to null without disturbing existing positional call sites' argument
    /// order, same reasoning as <c>ToggleAvailability</c>'s <c>connectionId</c> parameter.
    /// </remarks>
    [HttpPost("/event/{code}/finalize/final-dates/{finalDateId:int}/remove", Name = "EventRemoveFinalDate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFinalDate(string code, int finalDateId, CancellationToken cancellationToken, [FromForm] string? knownFinalDateIds = null)
    {
        var eventEntity = await GetTrackedEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return CreateEventNotFoundResult();
        }

        var participant = await GetCurrentParticipantAsync(eventEntity, currentUser: null, includeUserFallback: false, cancellationToken);
        if (participant is null)
        {
            return RedirectToRoute("EventSignIn", new { code = eventEntity.Code });
        }

        if (!await CanManageEventAsync(eventEntity, participant, cancellationToken))
        {
            return Forbid();
        }

        if (await FinalDatesAreStaleAsync(eventEntity.Id, knownFinalDateIds, cancellationToken))
        {
            // Keyed to RemoveFinalDateError, not string.Empty, so this renders next to the
            // "Final dates" card's remove control via asp-validation-for rather than in
            // _FinalizeCallTheDateCard's ModelOnly summary, which is for the add-date form.
            ModelState.AddModelError(nameof(EventFinalizeViewModel.RemoveFinalDateError), FinalDatesStaleErrorMessage);
            return View("Finalize", await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
        }

        var finalDate = await _db.EventFinalDates
            .SingleOrDefaultAsync(f => f.Id == finalDateId && f.EventId == eventEntity.Id, cancellationToken);

        if (finalDate is not null)
        {
            _db.EventFinalDates.Remove(finalDate);
            await _db.SaveChangesAsync(cancellationToken);

            await BroadcastFinalDatesChangedAsync(eventEntity, cancellationToken);
        }

        return RedirectToRoute("EventFinalize", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Parses a comma-separated <c>knownFinalDateIds</c> form field (the final-date id set the
    /// client had at page-load/last-render) into the set of ids it names, ignoring any entry that
    /// isn't a valid integer rather than rejecting the whole request over one bad token.
    /// </summary>
    private static HashSet<int> ParseKnownFinalDateIds(string? knownFinalDateIds)
    {
        if (string.IsNullOrWhiteSpace(knownFinalDateIds))
        {
            return [];
        }

        return knownFinalDateIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? (int?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
    }

    /// <summary>
    /// True if <paramref name="knownFinalDateIds"/> (the client's final-date id set as of its last
    /// render) no longer matches the event's current final-date ids — i.e. another
    /// Add/RemoveFinalDate call landed since this client last saw the list. See the feature spec's
    /// Design Decisions section: deliberately narrow (an id-set compare, no new column/migration),
    /// not the general "track event last-active" concept planned as its own separate future issue.
    /// </summary>
    /// <remarks>
    /// Known limitation, accepted as-is: this is a check-then-act read with no transaction/concurrency
    /// token tying it to the later write, so two requests racing within the same round-trip can both
    /// read as "not stale". Low severity — Add/Remove are independent insert/delete-by-id operations,
    /// so a race only means the stale-warning doesn't fire, not that data gets corrupted.
    /// </remarks>
    private async Task<bool> FinalDatesAreStaleAsync(int eventId, string? knownFinalDateIds, CancellationToken cancellationToken)
    {
        var knownIds = ParseKnownFinalDateIds(knownFinalDateIds);

        var currentIds = await _db.EventFinalDates
            .AsNoTracking()
            .Where(f => f.EventId == eventId)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        return !knownIds.SetEquals(currentIds);
    }

    /// <summary>
    /// Broadcasts the event's current final dates to every connected viewer of its Home/Finalize
    /// page. No exclusion of the acting connection (unlike ToggleAvailability's broadcast) — the
    /// actor's own page reloads via this action's redirect regardless.
    /// </summary>
    private async Task BroadcastFinalDatesChangedAsync(Event eventEntity, CancellationToken cancellationToken)
    {
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

        await _hub.Clients.Group(EventHub.GroupName(eventEntity.Code))
            .SendAsync("FinalDatesChanged", new { finalDates }, cancellationToken);
    }
}
