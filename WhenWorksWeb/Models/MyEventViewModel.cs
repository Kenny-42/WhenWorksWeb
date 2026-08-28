namespace WhenWorksWeb.Models;

/// <summary>
/// Represents a single participant record the current user created for an event on the My Events page.
/// </summary>
public sealed class MyEventParticipantViewModel
{
    /// <summary>
    /// The database id for the participant record.
    /// </summary>
    public required int ParticipantId { get; init; }

    /// <summary>
    /// The display name for the participant record.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// A hexadecimal color code (without the '#' symbol) representing the participant's personal color for this event.
    /// </summary>
    public required string Color { get; init; }
}

/// <summary>
/// Represents a single event row on the My Events page.
/// </summary>
public sealed class MyEventViewModel
{
    /// <summary>
    /// The database id for the event.
    /// </summary>
    public required int EventId { get; init; }

    /// <summary>
    /// All of the current user's participant records for this event.
    /// </summary>
    public required IReadOnlyList<MyEventParticipantViewModel> Participants { get; init; }

    /// <summary>
    /// The user id of the account that created the event.
    /// </summary>
    public string? CreatedByUserId { get; init; }

    /// <summary>
    /// The unique event code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// The event title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The emoji representing the event.
    /// </summary>
    public required string Emoji { get; init; }

    /// <summary>
    /// The event's description. Falls back to a default placeholder when no description was provided.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The total number of participants (across all users) who have joined this event.
    /// </summary>
    public required int TotalParticipantCount { get; init; }

    /// <summary>
    /// The URL used to send the user to the existing event sign-in page.
    /// </summary>
    public required string SignInUrl { get; init; }
}

/// <summary>
/// A single page of the current user's joined/created events, for the paginated My Events list.
/// </summary>
public sealed class MyEventsPageViewModel
{
    /// <summary>
    /// The events to display on the current page.
    /// </summary>
    public required IReadOnlyList<MyEventViewModel> Events { get; init; }

    /// <summary>
    /// The 1-based page number currently being displayed.
    /// </summary>
    public required int CurrentPage { get; init; }

    /// <summary>
    /// The total number of pages available, given <see cref="PageSize"/>.
    /// </summary>
    public required int TotalPages { get; init; }

    /// <summary>
    /// The total number of events the current user has joined or created, across all pages.
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// The maximum number of events displayed per page.
    /// </summary>
    public required int PageSize { get; init; }
}