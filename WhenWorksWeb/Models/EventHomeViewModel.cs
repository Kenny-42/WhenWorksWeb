namespace WhenWorksWeb.Models;

/// <summary>
/// View model for the event home page's Availability tab.
/// </summary>
/// <remarks>
/// Placeholder for now — the calendar/best-bets/status sidebar content described in
/// Spec/Features/FEATURES-event-home-page.ospec is a separate, larger follow-up; this only
/// carries the shared page chrome so the three-tab layout is fully navigable.
/// </remarks>
public sealed class EventHomeViewModel
{
    /// <summary>
    /// Shared page chrome data (badge, title/emoji, copyable code, settings shortcut, tab bar).
    /// </summary>
    public required EventHeaderViewModel Header { get; init; }
}
