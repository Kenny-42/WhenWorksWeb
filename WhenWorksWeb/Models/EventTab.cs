namespace WhenWorksWeb.Models;

/// <summary>
/// Identifies one of the three tabs on the event home page, used to highlight the active tab in
/// the shared header/tab-bar partial.
/// </summary>
public enum EventTab
{
    /// <summary>The Availability tab (calendar + best-bets/status sidebar).</summary>
    Availability,

    /// <summary>The People tab (participant roster).</summary>
    People,

    /// <summary>The Settings tab (event details, emoji, final-date selection, delete).</summary>
    Settings
}
