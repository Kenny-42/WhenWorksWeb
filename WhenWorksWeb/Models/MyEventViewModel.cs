namespace WhenWorksWeb.Models;

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
    /// The current user's participant id for this event, if they have one.
    /// </summary>
    public int? ParticipantId { get; init; }

    /// <summary>
    /// The current user's participant display name for this event, if they have one.
    /// </summary>
    public string ParticipantDisplayName { get; init; } = string.Empty;

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