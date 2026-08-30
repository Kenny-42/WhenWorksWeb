namespace WhenWorksWeb.Models;

/// <summary>
/// View model for the event home page's Settings tab: the "Shape the plan" edit form and the
/// Delete Event control. Final-date management moved to its own Finalize tab — see
/// <see cref="EventFinalizeViewModel"/>.
/// </summary>
public sealed class EventSettingsViewModel
{
    /// <summary>
    /// Shared page chrome data (badge, title/emoji, copyable code, settings shortcut, tab bar).
    /// </summary>
    public required EventHeaderViewModel Header { get; init; }

    /// <summary>
    /// Whether the current participant can manage this event (edit details, manage final dates,
    /// delete the event) — see the Organizer Permission Model section of the spec.
    /// </summary>
    public required bool CanManageEvent { get; init; }

    /// <summary>
    /// The event's current title, prefilled into the "Shape the plan" edit form.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The event's current description, prefilled into the "Shape the plan" edit form.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The event's current emoji, prefilled into the emoji-picker trigger button.
    /// </summary>
    public required string Emoji { get; init; }

    /// <summary>
    /// Whether the current participant can manage other organizers (promote/demote, toggle another
    /// organizer's <see cref="Participant.CanManageOrganizers"/> flag, delete the event) — see the
    /// Permission Model section of the organizer-permissions spec. Drives whether the "Organizer
    /// permissions" card is shown at all, not just whether its controls are enabled.
    /// </summary>
    public required bool CanManageOrganizers { get; init; }

    /// <summary>
    /// Every current organizer, for the pill list and the Demote dropdown. Empty when
    /// <see cref="CanManageOrganizers"/> is false — the card that would render this is hidden
    /// entirely, so it's never queried in that case.
    /// </summary>
    public required IReadOnlyList<EventOrganizerViewModel> Organizers { get; init; }

    /// <summary>
    /// Every current non-organizer participant, for the Promote dropdown. Empty when
    /// <see cref="CanManageOrganizers"/> is false, same reasoning as <see cref="Organizers"/>.
    /// </summary>
    public required IReadOnlyList<EventParticipantOptionViewModel> PromotableParticipants { get; init; }
}
