namespace WhenWorksWeb.Models;

/// <summary>
/// Identifies one of the four tabs on the event home page, used to highlight the active tab in
/// the shared header/tab-bar partial.
/// </summary>
public enum EventTab
{
    /// <summary>The Availability tab (calendar + best-bets/status/final-dates sidebar).</summary>
    Availability,

    /// <summary>The People tab (participant roster).</summary>
    People,

    /// <summary>The Finalize tab (suggestions + final-date selection).</summary>
    Finalize,

    /// <summary>The Settings tab (event details, emoji, delete).</summary>
    Settings
}
