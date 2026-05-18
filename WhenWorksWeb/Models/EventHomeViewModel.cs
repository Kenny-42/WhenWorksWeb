namespace WhenWorksWeb.Models;

/// <summary>
/// View model for the event home page.
/// </summary>
public sealed class EventHomeViewModel
{
    /// <summary>
    /// The unique 6 character code associated with the event.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// The title of the event.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The user's unique rejoin code that is used primarily for guests re-joining events.
    /// </summary>
    public required string RejoinCode { get; init; }
}