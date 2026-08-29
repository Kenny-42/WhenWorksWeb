using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// Displays the Settings tab for the event associated with the specified code: the "Shape the
    /// plan" edit form, the "Call the date" suggestions/final-dates card, and Delete Event.
    /// </summary>
    /// <remarks>The page is only accessible after a successful sign-in flow has issued a valid access cookie.</remarks>
    [HttpGet("/event/{code}/settings", Name = "EventSettings")]
    public async Task<IActionResult> Settings(string code, CancellationToken cancellationToken)
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

        return View(await BuildEventSettingsViewModelAsync(eventEntity, participant, cancellationToken));
    }

    /// <summary>
    /// Updates the event's title, description, and emoji. Organizer-only — the client-side
    /// <see cref="EventSettingsViewModel.CanManageEvent"/> flag only hides the form; this is the
    /// actual enforcement, re-checked against the database rather than trusted from the request.
    /// </summary>
    [HttpPost("/event/{code}/settings/details", Name = "EventUpdateDetails")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDetails(string code, [FromForm] string title, [FromForm] string? description, [FromForm] string? emoji, CancellationToken cancellationToken)
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

        var trimmedTitle = title?.Trim();
        if (string.IsNullOrEmpty(trimmedTitle) || trimmedTitle.Length > ModelConstants.EventTitleMaxLength)
        {
            ModelState.AddModelError(nameof(title), $"Title must be between 1 and {ModelConstants.EventTitleMaxLength} characters.");
            return View("Settings", await BuildEventSettingsViewModelAsync(eventEntity, participant, cancellationToken));
        }

        var trimmedDescription = description?.Trim();
        var trimmedEmoji = emoji?.Trim();

        eventEntity.Title = trimmedTitle;
        eventEntity.LastActiveAt = DateTimeOffset.UtcNow;

        var settings = await _db.EventSettings.SingleOrDefaultAsync(s => s.EventId == eventEntity.Id, cancellationToken);
        if (settings is null)
        {
            settings = new EventSettings
            {
                EventId = eventEntity.Id,
                Emoji = string.IsNullOrWhiteSpace(trimmedEmoji) ? ModelConstants.DefaultEventEmoji : trimmedEmoji
            };
            _db.EventSettings.Add(settings);
        }
        else if (!string.IsNullOrWhiteSpace(trimmedEmoji))
        {
            settings.Emoji = trimmedEmoji;
        }

        settings.Description = string.IsNullOrEmpty(trimmedDescription) ? null : trimmedDescription;

        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToRoute("EventSettings", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Deletes the entire event. Organizer-only, gated by a confirmation modal client-side.
    /// </summary>
    [HttpPost("/event/{code}/settings/delete", Name = "EventDelete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(string code, CancellationToken cancellationToken)
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

        _db.Events.Remove(eventEntity);
        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Builds the full Settings tab view model: shared chrome, current title/description/emoji,
    /// the Availability tab's calendar data (reused so the "Suggestions" sub-list is ranked by the
    /// same shared best-bets script, no second query shape needed), and the organizer's current
    /// final date entries.
    /// </summary>
    private async Task<EventSettingsViewModel> BuildEventSettingsViewModelAsync(Event eventEntity, Participant participant, CancellationToken cancellationToken)
    {
        var settings = await _db.EventSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.EventId == eventEntity.Id, cancellationToken);

        var emoji = settings?.Emoji ?? ModelConstants.DefaultEventEmoji;
        var canManageEvent = await CanManageEventAsync(eventEntity, participant, cancellationToken);
        var calendar = await BuildEventCalendarAsync(eventEntity, participant, cancellationToken);

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

        return new EventSettingsViewModel
        {
            Header = BuildEventHeader(eventEntity, emoji, EventTab.Settings),
            CanManageEvent = canManageEvent,
            Title = eventEntity.Title,
            Description = string.IsNullOrWhiteSpace(settings?.Description) ? null : settings.Description,
            Emoji = emoji,
            Calendar = calendar,
            FinalDates = finalDates
        };
    }
}
