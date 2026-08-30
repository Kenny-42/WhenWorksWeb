using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

/// <summary>
/// Shared plumbing for the three tabs (Availability/People/Settings) of the event home page:
/// building the header/tab-bar chrome data and resolving the current participant's organizer
/// permission, so each tab's own action doesn't repeat this logic.
/// </summary>
public partial class EventsController
{
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
    /// Builds the shared header/tab-bar view model rendered identically at the top of all three tabs.
    /// </summary>
    private static EventHeaderViewModel BuildEventHeader(Event eventEntity, string emoji, EventTab activeTab)
    {
        return new EventHeaderViewModel
        {
            Code = eventEntity.Code,
            Title = eventEntity.Title,
            Emoji = emoji,
            ActiveTab = activeTab
        };
    }

    /// <summary>
    /// Returns whether the given participant may perform organizer-only actions on the event
    /// (editing event details, managing final dates, deleting the event): true if they're
    /// currently flagged as an organizer, or — so a guest-created or organizer-less event never
    /// gets permanently locked — if the event has no organizer at all right now.
    /// </summary>
    private async Task<bool> CanManageEventAsync(Event eventEntity, Participant currentParticipant, CancellationToken cancellationToken)
    {
        if (currentParticipant.IsOrganizer)
        {
            return true;
        }

        return !await _db.Participants
            .AsNoTracking()
            .AnyAsync(p => p.EventId == eventEntity.Id && p.IsOrganizer, cancellationToken);
    }

    /// <summary>
    /// Returns whether the given participant may perform manage-organizers actions on the event
    /// (promoting/demoting other organizers, toggling another organizer's
    /// <see cref="Participant.CanManageOrganizers"/> flag, deleting the event): true if they
    /// currently hold the flag themselves, or — narrower than <see cref="CanManageEventAsync"/>'s
    /// own zero-organizer fallback — if the event has no <see cref="Participant.CanManageOrganizers"/>
    /// holder at all right now, in which case these actions fall open to every current organizer
    /// instead of every participant.
    /// </summary>
    private async Task<bool> CanManageOrganizersAsync(Event eventEntity, Participant currentParticipant, CancellationToken cancellationToken)
    {
        if (currentParticipant.CanManageOrganizers)
        {
            return true;
        }

        var anyoneHoldsFlag = await _db.Participants
            .AsNoTracking()
            .AnyAsync(p => p.EventId == eventEntity.Id && p.CanManageOrganizers, cancellationToken);

        return !anyoneHoldsFlag && currentParticipant.IsOrganizer;
    }
}
