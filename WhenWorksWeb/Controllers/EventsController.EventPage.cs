using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

/// <summary>
/// Shared plumbing for the three tabs (Availability/People/Settings) of the event home page:
/// building the header/tab-bar chrome data, resolving the current participant's organizer
/// permission, and authorizing the Settings tab's mutating actions — so each tab's own action
/// doesn't repeat this logic.
/// </summary>
public partial class EventsController
{
    /// <summary>
    /// The event and current participant resolved by <see cref="AuthorizeEventActionAsync"/> once
    /// both exist and the caller's permission check has passed.
    /// </summary>
    private readonly record struct AuthorizedEventContext(Event Event, Participant Participant);

    /// <summary>
    /// Shared "load the tracked event, resolve the current participant, and enforce a permission
    /// check" prologue used by every Settings-tab mutation (<c>UpdateDetails</c>/<c>DeleteEvent</c>
    /// in <c>EventsController.Settings.cs</c>, and the promote/demote/toggle actions in
    /// <c>EventsController.Organizers.cs</c>) — they all need the exact same not-found, sign-in
    /// redirect, and <c>Forbid()</c> handling and previously hand-rolled it individually. Returns
    /// the resolved context on success; on failure, returns the <see cref="IActionResult"/> the
    /// caller should return immediately instead, with <c>Context</c> null.
    /// </summary>
    private async Task<(AuthorizedEventContext? Context, IActionResult? Failure)> AuthorizeEventActionAsync(
        string code,
        Func<Event, Participant, CancellationToken, Task<bool>> hasPermissionAsync,
        CancellationToken cancellationToken)
    {
        var eventEntity = await GetTrackedEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return (null, CreateEventNotFoundResult());
        }

        var participant = await GetCurrentParticipantAsync(eventEntity, currentUser: null, includeUserFallback: false, cancellationToken);
        if (participant is null)
        {
            return (null, RedirectToRoute("EventSignIn", new { code = eventEntity.Code }));
        }

        if (!await hasPermissionAsync(eventEntity, participant, cancellationToken))
        {
            return (null, Forbid());
        }

        return (new AuthorizedEventContext(eventEntity, participant), null);
    }

    /// <summary>
    /// Loads the event's emoji, falling back to the default when no <see cref="EventSettings"/>
    /// row exists yet.
    /// </summary>
    private async Task<string> GetEventEmojiAsync(Event eventEntity, CancellationToken cancellationToken)
    {
        var emoji = await _db.EventSettings
            .AsNoTracking()
            .Where(s => s.EventId == eventEntity.Id)
            .Select(s => s.Emoji)
            .SingleOrDefaultAsync(cancellationToken);

        return emoji ?? ModelConstants.DefaultEventEmoji;
    }

    /// <summary>
    /// Loads the event's description for the shared page header's description card, queried on
    /// its own (rather than reusing an already-loaded <see cref="EventSettings"/> row) for the
    /// three tabs that don't otherwise load one. See <see cref="ResolveHeaderDescription"/> for
    /// the null/empty-string meaning.
    /// </summary>
    private async Task<string?> GetEventDescriptionAsync(Event eventEntity, CancellationToken cancellationToken)
    {
        var description = await _db.EventSettings
            .AsNoTracking()
            .Where(s => s.EventId == eventEntity.Id)
            .Select(s => s.Description)
            .SingleOrDefaultAsync(cancellationToken);

        return ResolveHeaderDescription(description);
    }

    /// <summary>
    /// Resolves a raw <see cref="EventSettings.Description"/> value (or null, if no
    /// <see cref="EventSettings"/> row exists yet) into what the header's description card should
    /// render. Null (never customized) resolves to the default placeholder text. Empty string
    /// means an organizer previously set a description and then explicitly cleared it (see
    /// <c>UpdateDetails</c> in <c>EventsController.Settings.cs</c>) — that resolves to null here so
    /// the caller hides the card entirely rather than falling back to the default text.
    /// </summary>
    private static string? ResolveHeaderDescription(string? rawDescription)
    {
        if (rawDescription is null)
        {
            return ModelConstants.DefaultEventDescription;
        }

        return rawDescription.Length == 0 ? null : rawDescription;
    }

    /// <summary>
    /// Builds the shared header/tab-bar view model rendered identically at the top of all three tabs.
    /// </summary>
    private static EventHeaderViewModel BuildEventHeader(Event eventEntity, string emoji, EventTab activeTab, string? description = null)
    {
        return new EventHeaderViewModel
        {
            Code = eventEntity.Code,
            Title = eventEntity.Title,
            Emoji = emoji,
            ActiveTab = activeTab,
            Description = description
        };
    }

    /// <summary>
    /// Returns whether the event currently has zero participants flagged
    /// <see cref="Participant.IsOrganizer"/> — the shared trigger for both
    /// <see cref="CanManageEventAsync"/>'s and <see cref="CanManageOrganizersAsync"/>'s
    /// zero-organizer fallback.
    /// </summary>
    private async Task<bool> HasNoOrganizersAsync(Event eventEntity, CancellationToken cancellationToken)
    {
        return !await _db.Participants
            .AsNoTracking()
            .AnyAsync(p => p.EventId == eventEntity.Id && p.IsOrganizer, cancellationToken);
    }

    /// <summary>
    /// Returns whether the given participant may perform organizer-only actions on the event
    /// (editing event details, managing final dates): true if they're currently flagged as an
    /// organizer, or — so a guest-created or organizer-less event never gets permanently locked —
    /// if the event has no organizer at all right now.
    /// </summary>
    private async Task<bool> CanManageEventAsync(Event eventEntity, Participant currentParticipant, CancellationToken cancellationToken)
    {
        return currentParticipant.IsOrganizer || await HasNoOrganizersAsync(eventEntity, cancellationToken);
    }

    /// <summary>
    /// Returns whether the given participant may perform manage-organizers actions on the event
    /// (promoting/demoting other organizers, toggling another organizer's
    /// <see cref="Participant.CanManageOrganizers"/> flag, deleting the event): true if they
    /// currently hold the flag themselves; if the event has no organizer at all right now (same
    /// zero-holder trigger as <see cref="CanManageEventAsync"/>), these actions fall open to
    /// every participant too — an organizer-less event must always have some way for someone to
    /// promote a new organizer or delete it, not just edit its details; or, narrower, if the
    /// event has at least one organizer but nobody currently holds
    /// <see cref="Participant.CanManageOrganizers"/> (e.g. the sole holder demoted themselves),
    /// these actions fall open to every current organizer instead of every participant.
    /// </summary>
    private async Task<bool> CanManageOrganizersAsync(Event eventEntity, Participant currentParticipant, CancellationToken cancellationToken)
    {
        if (currentParticipant.CanManageOrganizers)
        {
            return true;
        }

        if (await HasNoOrganizersAsync(eventEntity, cancellationToken))
        {
            return true;
        }

        if (!currentParticipant.IsOrganizer)
        {
            return false;
        }

        return !await _db.Participants
            .AsNoTracking()
            .AnyAsync(p => p.EventId == eventEntity.Id && p.CanManageOrganizers, cancellationToken);
    }
}
