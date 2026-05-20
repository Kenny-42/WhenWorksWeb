namespace WhenWorksWeb.Models;

/// <summary>
/// Represents a single event row on the My Events page.
/// </summary>
public sealed class MyEventViewModel
{
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