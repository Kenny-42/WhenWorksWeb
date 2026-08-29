namespace WhenWorksWeb.Models;

/// <summary>
/// One organizer-chosen final date (or date range) on the Settings tab's "Call the date" card.
/// </summary>
public sealed class EventFinalDateViewModel
{
    /// <summary>
    /// The database id of the <see cref="EventFinalDate"/> row, posted back to remove this entry.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The first (or only, if <see cref="EndDate"/> is null) day of this final date entry.
    /// </summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>
    /// The last day of this final date entry, for a date range. Null for a single-day entry.
    /// </summary>
    public DateOnly? EndDate { get; init; }
}

/// <summary>
/// View model for the event home page's Settings tab: the "Shape the plan" edit form, the "Call
/// the date" suggestions/final-dates card, and the Delete Event control.
/// </summary>
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

    /// <summary>
    /// The event's current title, prefilled into the "Shape the plan" edit form.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The event's current description, prefilled into the "Shape the plan" edit form.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The event's current emoji, prefilled into the emoji-picker trigger button.
    /// </summary>
    public required string Emoji { get; init; }

    /// <summary>
    /// The same calendar data the Availability tab uses, so this tab's "Suggestions" sub-list can
    /// be ranked client-side by the shared best-bets script without a second query shape.
    /// </summary>
    public required EventCalendarViewModel Calendar { get; init; }

    /// <summary>
    /// The organizer's current final date entries, ordered by start date.
    /// </summary>
    public required IReadOnlyList<EventFinalDateViewModel> FinalDates { get; init; }
}
