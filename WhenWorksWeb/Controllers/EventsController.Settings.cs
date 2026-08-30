using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// Displays the Settings tab for the event associated with the specified code: the "Shape the
    /// plan" edit form and Delete Event.
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

        if (trimmedDescription?.Length > ModelConstants.EventDescriptionMaxLength)
        {
            ModelState.AddModelError(nameof(description), $"Description must be {ModelConstants.EventDescriptionMaxLength} characters or fewer.");
        }

        if (trimmedEmoji?.Length > ModelConstants.EventEmojiMaxLength)
        {
            ModelState.AddModelError(nameof(emoji), $"Emoji must be {ModelConstants.EventEmojiMaxLength} characters or fewer.");
        }

        if (!ModelState.IsValid)
        {
            return View("Settings", await BuildEventSettingsViewModelAsync(eventEntity, participant, cancellationToken));
        }

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
    /// Deletes the entire event. Gated by <see cref="CanManageOrganizersAsync"/> (not plain
    /// <see cref="CanManageEventAsync"/> organizer status), and by a confirmation modal
    /// client-side.
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

        if (!await CanManageOrganizersAsync(eventEntity, participant, cancellationToken))
        {
            return Forbid();
        }

        _db.Events.Remove(eventEntity);
        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Builds the Settings tab view model: shared chrome plus the current title/description/
    /// emoji, and — only for a participant who holds <see cref="Participant.CanManageOrganizers"/>
    /// — the "Organizer permissions" card's pill list and dropdown options (see
    /// <see cref="BuildOrganizerManagementDataAsync"/> in <c>EventsController.Organizers.cs</c>).
    /// Final-date management lives on its own Finalize tab now — see
    /// <see cref="BuildEventFinalizeViewModelAsync"/> in <c>EventsController.Finalize.cs</c>.
    /// </summary>
    private async Task<EventSettingsViewModel> BuildEventSettingsViewModelAsync(Event eventEntity, Participant participant, CancellationToken cancellationToken)
    {
        var settings = await _db.EventSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.EventId == eventEntity.Id, cancellationToken);

        var emoji = settings?.Emoji ?? ModelConstants.DefaultEventEmoji;
        var canManageEvent = await CanManageEventAsync(eventEntity, participant, cancellationToken);
        var canManageOrganizers = await CanManageOrganizersAsync(eventEntity, participant, cancellationToken);

        var organizers = Array.Empty<EventOrganizerViewModel>() as IReadOnlyList<EventOrganizerViewModel>;
        var promotableParticipants = Array.Empty<EventParticipantOptionViewModel>() as IReadOnlyList<EventParticipantOptionViewModel>;

        if (canManageOrganizers)
        {
            (organizers, promotableParticipants) = await BuildOrganizerManagementDataAsync(eventEntity, participant, cancellationToken);
        }

        return new EventSettingsViewModel
        {
            Header = BuildEventHeader(eventEntity, emoji, EventTab.Settings),
            CanManageEvent = canManageEvent,
            Title = eventEntity.Title,
            Description = string.IsNullOrWhiteSpace(settings?.Description) ? null : settings.Description,
            Emoji = emoji,
            CanManageOrganizers = canManageOrganizers,
            Organizers = organizers,
            PromotableParticipants = promotableParticipants
        };
    }
}
