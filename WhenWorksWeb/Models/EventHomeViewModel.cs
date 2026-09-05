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
    /// the live "Final dates" card's "Choose another date" link is shown, and whether the calendar
    /// card header's timezone picker is enabled (shown either way, since every participant should
    /// see what zone the calendar reflects — see the event-timezone feature spec).
    /// </summary>
    public required bool CanManageEvent { get; init; }

    /// <summary>
    /// The event's current IANA timezone id, preselected in the calendar card header's timezone
    /// picker.
    /// </summary>
    public required string CurrentTimeZoneId { get; init; }

    /// <summary>
    /// The full grouped, offset-sorted IANA zone list for the timezone picker's <c>&lt;select&gt;</c>
    /// — see <see cref="Services.TimeZoneOptionsProvider.GetGroupedOptions"/>.
    /// </summary>
    public required IReadOnlyList<TimeZoneGroupViewModel> TimeZoneGroups { get; init; }
}
