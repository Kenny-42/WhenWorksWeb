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
    public async Task<IActionResult> UpdateDetails(string code, EventUpdateDetailsViewModel model, CancellationToken cancellationToken)
    {
        var (context, failure) = await AuthorizeEventActionAsync(code, CanManageEventAsync, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var (eventEntity, participant) = context!.Value;

        // Trim before validating, same as the account-settings pages, so e.g. a title of all
        // whitespace fails [Required]/[StringLength]'s MinimumLength rather than being accepted
        // and only then trimmed down to an empty string.
        model.Title = model.Title.Trim();
        model.Description = model.Description?.Trim();
        model.Emoji = model.Emoji?.Trim();

        // Model binding already ran validation once, against the raw untrimmed values, before
        // this action even started -- e.g. a title with incidental leading/trailing whitespace
        // that pushes its raw length past EventTitleMaxLength fails that automatic pass and
        // leaves a stale error sitting in ModelState even though the trimmed value above is
        // perfectly valid. Clear that stale state before re-validating the trimmed model, or
        // TryValidateModel below can never succeed for a value it would otherwise accept.
        // ModelState.Clear() (not ClearValidationState, which is keyed by ModelState key --
        // "Title"/"Description"/"Emoji" here, not the "model" parameter name) is safe since
        // nothing else in this action reads or depends on prior ModelState entries.
        ModelState.Clear();

        if (!TryValidateModel(model))
        {
            return View("Settings", await BuildEventSettingsViewModelAsync(eventEntity, participant, cancellationToken));
        }

        eventEntity.Title = model.Title;
        eventEntity.LastActiveAt = DateTimeOffset.UtcNow;

        var settings = await _db.EventSettings.SingleOrDefaultAsync(s => s.EventId == eventEntity.Id, cancellationToken);
        if (settings is null)
        {
            settings = new EventSettings
            {
                EventId = eventEntity.Id,
                Emoji = string.IsNullOrWhiteSpace(model.Emoji) ? ModelConstants.DefaultEventEmoji : model.Emoji
            };
            _db.EventSettings.Add(settings);
        }
        else if (!string.IsNullOrWhiteSpace(model.Emoji))
        {
            settings.Emoji = model.Emoji;
        }

        // Null leaves the description uncustomized (Event Home falls back to the default
        // placeholder text — see GetEventDescriptionAsync); but if it was previously non-null —
        // real text, or already the "explicitly cleared" empty-string sentinel itself — and this
        // save blanks it, that's (re-)stored as an empty string instead of null so Event Home can
        // tell "never customized" apart from "explicitly cleared" and hide the description card
        // entirely rather than reverting to the default text. Checked against settings.Description
        // (not-null, not IsNullOrEmpty) specifically so a blank resubmit over an already-cleared
        // description doesn't get read as "nothing to clear" and wrongly reset back to null.
        var trimmedDescription = string.IsNullOrEmpty(model.Description) ? null : model.Description;
        settings.Description = trimmedDescription is null && settings.Description is not null
            ? string.Empty
            : trimmedDescription;

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
        var (context, failure) = await AuthorizeEventActionAsync(code, CanManageOrganizersAsync, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var (eventEntity, _) = context!.Value;

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
            Header = BuildEventHeader(eventEntity, emoji, EventTab.Settings, ResolveHeaderDescription(settings?.Description)),
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
