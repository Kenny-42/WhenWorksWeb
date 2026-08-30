namespace WhenWorksWeb.Models;

/// <summary>
/// View model for one row of the Settings tab's "Organizer permissions" card — one entry per
/// participant with <see cref="Participant.IsOrganizer"/> set, rendered as a pill with a "Full
/// access" toggle. Also doubles as the Demote dropdown's option list, since it's the same set of
/// participants. See <see cref="EventSettingsViewModel.Organizers"/>.
/// </summary>
public sealed class EventOrganizerViewModel
{
    /// <summary>
    /// The event's code, so the pill's own "Full access" toggle form (rendered standalone by
    /// <c>_OrganizerPill.cshtml</c>, without the rest of the Settings page around it) can build
    /// its route without needing the page's other view-model data passed alongside it.
    /// </summary>
    public required string EventCode { get; init; }

    /// <summary>The organizer's participant id.</summary>
    public required int Id { get; init; }

    /// <summary>The organizer's display name for this event.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The organizer's personal color for this event (hex, without the '#').</summary>
    public required string Color { get; init; }

    /// <summary>
    /// Whether this organizer currently holds <see cref="Participant.CanManageOrganizers"/> —
    /// drives the pill's "Full access" toggle state.
    /// </summary>
    public required bool CanManageOrganizers { get; init; }

    /// <summary>Whether this row is the signed-in participant viewing the page.</summary>
    public required bool IsCurrentParticipant { get; init; }
}
