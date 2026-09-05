namespace WhenWorksWeb.Models;

/// <summary>
/// A single selectable timezone in the event timezone picker's grouped <c>&lt;select&gt;</c>: an
/// IANA id and its ready-to-render label (e.g. <c>"(UTC−05:00) America/New_York"</c>).
/// </summary>
public sealed class TimeZoneOptionViewModel
{
    /// <summary>
    /// The IANA timezone id this option submits as <see cref="Event.TimeZoneId"/>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The option's display label: its current UTC offset followed by its id.
    /// </summary>
    public required string Label { get; init; }
}

/// <summary>
/// One <c>&lt;optgroup&gt;</c> of the event timezone picker: every zone in one continent/area,
/// already sorted by UTC offset. See <see cref="Services.TimeZoneOptionsProvider.GetGroupedOptions"/>.
/// </summary>
public sealed class TimeZoneGroupViewModel
{
    /// <summary>
    /// The continent/area this group is labeled with (e.g. "America", "Europe", or "Other" for an
    /// id with no area segment, like "UTC").
    /// </summary>
    public required string GroupLabel { get; init; }

    /// <summary>
    /// The zones in this group, sorted by UTC offset then by id.
    /// </summary>
    public required IReadOnlyList<TimeZoneOptionViewModel> Options { get; init; }
}
