namespace WhenWorksWeb.Models;

/// <summary>
/// View model for the event home page's Settings tab.
/// </summary>
/// <remarks>
/// Placeholder for now — the "Shape the plan"/"Call the date"/Delete Event cards described in
/// Spec/Features/FEATURES-event-home-page.ospec are a separate, larger follow-up; this only
/// carries the shared page chrome and the current participant's permission flag so the
/// three-tab layout is fully navigable.
/// </remarks>
public sealed class EventSettingsViewModel
{
    /// <summary>
    /// Shared page chrome data (badge, title/emoji, copyable code, settings shortcut, tab bar).
    /// </summary>
    public required EventHeaderViewModel Header { get; init; }

    /// <summary>
    /// Whether the current participant can manage this event (edit details, manage final dates,
    /// delete the event) — see the Organizer Permission Model section of the spec.
    /// </summary>
    public required bool CanManageEvent { get; init; }
}
