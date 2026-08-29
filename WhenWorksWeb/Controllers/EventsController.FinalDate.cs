using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// Adds an organizer-chosen final date (or date range) to the event. Organizer-only, and
    /// entirely independent of <see cref="EventDate"/>/<see cref="ParticipantAvailability"/> — see
    /// the Schema Changes section of the feature spec for why the two are deliberately decoupled.
    /// </summary>
    [HttpPost("/event/{code}/finalize/final-dates", Name = "EventAddFinalDate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFinalDate(string code, [FromForm] string startDate, [FromForm] string? endDate, CancellationToken cancellationToken)
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

        if (!DateOnly.TryParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
        {
            return BadRequest();
        }

        DateOnly? end = null;
        if (!string.IsNullOrWhiteSpace(endDate))
        {
            if (!DateOnly.TryParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEnd))
            {
                return BadRequest();
            }

            if (parsedEnd < start)
            {
                ModelState.AddModelError(nameof(endDate), "End date must be on or after the start date.");
                return View("Finalize", await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
            }

            end = parsedEnd;
        }

        _db.EventFinalDates.Add(new EventFinalDate
        {
            EventId = eventEntity.Id,
            StartDate = start,
            EndDate = end
        });
        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToRoute("EventFinalize", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Removes an organizer-chosen final date entry from the event. Organizer-only; scoped to the
    /// requested event so one event's organizer can't remove another event's row by guessing an id.
    /// </summary>
    [HttpPost("/event/{code}/finalize/final-dates/{finalDateId:int}/remove", Name = "EventRemoveFinalDate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFinalDate(string code, int finalDateId, CancellationToken cancellationToken)
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

        var finalDate = await _db.EventFinalDates
            .SingleOrDefaultAsync(f => f.Id == finalDateId && f.EventId == eventEntity.Id, cancellationToken);

        if (finalDate is not null)
        {
            _db.EventFinalDates.Remove(finalDate);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return RedirectToRoute("EventFinalize", new { code = eventEntity.Code });
    }
}
