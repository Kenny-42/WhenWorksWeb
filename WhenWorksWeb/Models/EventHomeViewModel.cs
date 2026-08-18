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
}