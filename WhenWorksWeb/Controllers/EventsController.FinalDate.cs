using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
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
