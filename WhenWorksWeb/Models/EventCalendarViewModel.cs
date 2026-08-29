namespace WhenWorksWeb.Models;

/// <summary>
/// A single participant entry sent down for the Availability tab's calendar: enough to render
/// that participant's legend swatch and to color their wedge/marker on any date they've picked.
/// </summary>
public sealed class EventCalendarParticipantViewModel
{
    /// <summary>
    /// The participant's database id, used to match against <see cref="EventCalendarDateViewModel.ParticipantIds"/>.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The participant's display name, shown in the legend.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// A hexadecimal color code (without the '#' symbol) representing the participant's personal color for this event.
    /// </summary>
    public required string Color { get; init; }
}

/// <summary>
/// One candidate date that at least one participant has marked themselves available on. Dates
/// with zero availability marks are omitted entirely — the calendar grid itself is generated
/// client-side from calendar math, not from this list, so an absent date simply renders as an
/// empty, still-clickable cell.
/// </summary>
public sealed class EventCalendarDateViewModel
{
    /// <summary>
    /// The calendar day this entry applies to.
    /// </summary>
    public required DateOnly Date { get; init; }

    /// <summary>
    /// The ids of every participant who has marked themselves available on this date.
    /// </summary>
    public required IReadOnlyList<int> ParticipantIds { get; init; }
}

/// <summary>
/// The data needed to render the Availability tab's calendar, legend, best-bets card, and status
/// card entirely client-side (including after a click toggles a date), so month paging never
/// needs a server round trip. Serialized to JSON into a data attribute on the calendar grid.
/// </summary>
public sealed class EventCalendarViewModel
{
    /// <summary>
    /// The first day of the month initially shown when the page loads (the current month).
    /// </summary>
    public required DateOnly InitialMonth { get; init; }

    /// <summary>
    /// The first day of the earliest month the client is allowed to page back to.
    /// </summary>
    public required DateOnly WindowStartMonth { get; init; }

    /// <summary>
    /// The first day of the latest month the client is allowed to page forward to.
    /// </summary>
    public required DateOnly WindowEndMonth { get; init; }

    /// <summary>
    /// The database id of the participant currently viewing the page, used to render their own
    /// "my pick" marker and to color their own legend/status entries.
    /// </summary>
    public required int CurrentParticipantId { get; init; }

    /// <summary>
    /// Every participant in the event, for the legend and for coloring wedges/best-bets dots.
    /// </summary>
    public required IReadOnlyList<EventCalendarParticipantViewModel> Participants { get; init; }

    /// <summary>
    /// Every candidate date with at least one participant available on it.
    /// </summary>
    public required IReadOnlyList<EventCalendarDateViewModel> Dates { get; init; }
}
