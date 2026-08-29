namespace WhenWorksWeb.Models;

/// <summary>
/// Represents a single participant row on the event's People tab.
/// </summary>
public sealed class EventPersonViewModel
{
    /// <summary>
    /// The display name for the participant record.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// A hexadecimal color code (without the '#' symbol) representing the participant's personal color for this event.
    /// </summary>
    public required string Color { get; init; }

    /// <summary>
    /// Whether this participant currently has organizer permissions for the event.
    /// </summary>
    public required bool IsOrganizer { get; init; }

    /// <summary>
    /// Whether this row is the currently signed-in participant, for the "You" suffix.
    /// </summary>
    public required bool IsCurrentParticipant { get; init; }

    /// <summary>
    /// The number of candidate dates this participant has marked themselves available on.
    /// </summary>
    public required int DatesPickedCount { get; init; }
}

/// <summary>
/// View model for the event's People tab: one page of its participant roster, organizers first
/// then alphabetically by display name.
/// </summary>
public sealed class EventPeopleViewModel
{
    /// <summary>
    /// Shared page chrome data (badge, title/emoji, copyable code, settings shortcut, tab bar).
    /// </summary>
    public required EventHeaderViewModel Header { get; init; }

    /// <summary>
    /// The participants to display on the current page.
    /// </summary>
    public required IReadOnlyList<EventPersonViewModel> Participants { get; init; }

    /// <summary>
    /// The 1-based page number currently being displayed.
    /// </summary>
    public required int CurrentPage { get; init; }

    /// <summary>
    /// The total number of pages available, given <see cref="PageSize"/>.
    /// </summary>
    public required int TotalPages { get; init; }

    /// <summary>
    /// The total number of participants in the event, across all pages.
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// The maximum number of participants displayed per page.
    /// </summary>
    public required int PageSize { get; init; }
}
