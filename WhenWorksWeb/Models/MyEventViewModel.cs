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
    /// The URL used to send the user to the existing event sign-in page.
    /// </summary>
    public required string SignInUrl { get; init; }
}