namespace WhenWorksWeb.Models;

/// <summary>
/// Data shared by the page chrome (badge, title/emoji, copyable code, settings shortcut, tab bar)
/// rendered identically at the top of all three event-page tabs by the <c>_EventHeader</c> partial.
/// </summary>
public sealed class EventHeaderViewModel
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
    /// The emoji representing the event.
    /// </summary>
    public required string Emoji { get; init; }

    /// <summary>
    /// Which tab is currently active, so the tab bar can highlight it.
    /// </summary>
    public required EventTab ActiveTab { get; init; }
}
