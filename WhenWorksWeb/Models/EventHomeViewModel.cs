namespace WhenWorksWeb.Models;

/// <summary>
/// View model for the event home page's Availability tab: the shared page chrome plus the
/// calendar data needed to render the month grid, legend, best-bets card, and status card.
/// </summary>
public sealed class EventHomeViewModel
{
    /// <summary>
    /// Shared page chrome data (badge, title/emoji, copyable code, settings shortcut, tab bar).
    /// </summary>
    public required EventHeaderViewModel Header { get; init; }

    /// <summary>
    /// The calendar data for the Availability tab, rendered/updated entirely client-side.
    /// </summary>
    public required EventCalendarViewModel Calendar { get; init; }

    /// <summary>
    /// Whether the current participant can manage this event (edit details, manage final dates,
    /// delete the event) — see the Organizer Permission Model section of the spec. Drives whether
    /// the live "Final dates" card's "Choose another date" link is shown.
    /// </summary>
    public required bool CanManageEvent { get; init; }
}
