using WhenWorksWeb.Models;

namespace WhenWorksWeb.Services;

/// <summary>
/// Builds the grouped, offset-sorted list of IANA timezone options for the event timezone picker
/// (see the Availability tab's calendar card header), and resolves a submitted id against the
/// same canonical set an update is allowed to pick from.
/// </summary>
/// <remarks>
/// Registered as a singleton in <c>Program.cs</c>, unlike this codebase's usual scoped services,
/// since the canonical IANA id set it resolves at construction time is static OS/ICU data that
/// doesn't change per request -- see <see cref="_ianaZones"/>.
///
/// <see cref="TimeZoneInfo.GetSystemTimeZones"/> returns Windows time zone ids (e.g. "Eastern
/// Standard Time") rather than IANA ids on a Windows host, unlike on Linux/macOS where the OS's own
/// ids already are IANA ids -- <see cref="TimeZoneInfo.HasIanaId"/> tells them apart, and
/// <see cref="TimeZoneInfo.TryConvertWindowsIdToIanaId(string, out string?)"/> converts the former.
/// This matters here because the feature spec commits to storing/displaying IANA ids specifically
/// (<see cref="Event.TimeZoneId"/>), regardless of which OS the app happens to run on.
/// </remarks>
public sealed class TimeZoneOptionsProvider
{
    /// <summary>
    /// The canonical IANA id -&gt; resolved <see cref="TimeZoneInfo"/> map this instance validates
    /// and lists against, resolved once at construction rather than re-enumerated on every call --
    /// see this class's remarks. Deliberately narrower than <see cref="TimeZoneInfo.TryFindSystemTimeZoneById"/>
    /// (which also accepts Windows ids like "Eastern Standard Time" on any platform): only an id
    /// that appears here, i.e. an actual member of the IANA set the picker offers, is considered
    /// valid, since <see cref="Event.TimeZoneId"/> commits to storing IANA ids specifically.
    /// </summary>
    private readonly Dictionary<string, TimeZoneInfo> _ianaZones;

    public TimeZoneOptionsProvider()
    {
        _ianaZones = TimeZoneInfo.GetSystemTimeZones()
            .Select(zone => (Id: ResolveIanaId(zone), Zone: zone))
            .Where(z => z.Id is not null)
            .GroupBy(z => z.Id!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Zone, StringComparer.Ordinal);
    }

    /// <summary>
    /// Builds the full canonical IANA zone list, grouped by continent/area (the segment before the
    /// first '/' in the id, e.g. "America", "Europe"; ids with no '/', like "UTC", fall into an
    /// "Other" group) and sorted by current UTC offset within each group, then by id. Groups are
    /// sorted alphabetically by label.
    /// </summary>
    public IReadOnlyList<TimeZoneGroupViewModel> GetGroupedOptions()
    {
        var now = DateTimeOffset.UtcNow;

        return _ianaZones
            .Select(kvp => new { Id = kvp.Key, Offset = kvp.Value.GetUtcOffset(now) })
            .GroupBy(z => GroupLabelFor(z.Id))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new TimeZoneGroupViewModel
            {
                GroupLabel = g.Key,
                Options = g
                    .OrderBy(z => z.Offset)
                    .ThenBy(z => z.Id, StringComparer.Ordinal)
                    .Select(z => new TimeZoneOptionViewModel
                    {
                        Id = z.Id,
                        Label = $"({FormatOffset(z.Offset)}) {z.Id}"
                    })
                    .ToList()
            })
            .ToList();
    }

    /// <summary>
    /// Returns whether <paramref name="timeZoneId"/> is a member of the canonical IANA id set this
    /// instance resolved at construction -- the server-side check backing <c>UpdateTimeZone</c> and
    /// <c>Create</c>'s browser-detected id, since the dropdown's own options are only a client-side
    /// convenience and nothing stops a request from posting an arbitrary string.
    /// </summary>
    public bool IsValidTimeZoneId(string? timeZoneId)
    {
        return !string.IsNullOrWhiteSpace(timeZoneId) && _ianaZones.ContainsKey(timeZoneId);
    }

    /// <summary>
    /// Resolves a system <see cref="TimeZoneInfo"/> to its IANA id, converting from a Windows id
    /// first if needed (see this class's remarks). Returns null for the rare id that can't be
    /// resolved either way, so it's dropped from the set rather than shown/accepted incorrectly.
    /// </summary>
    private static string? ResolveIanaId(TimeZoneInfo timeZone)
    {
        if (timeZone.HasIanaId)
        {
            return timeZone.Id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out var ianaId) ? ianaId : null;
    }

    /// <summary>
    /// The continent/area segment of an IANA id (e.g. "America" from "America/New_York"), or
    /// "Other" for an id with no '/' (e.g. "UTC").
    /// </summary>
    private static string GroupLabelFor(string ianaId)
    {
        var slashIndex = ianaId.IndexOf('/');
        return slashIndex < 0 ? "Other" : ianaId[..slashIndex];
    }

    /// <summary>
    /// Formats a UTC offset as e.g. "UTC−05:00"/"UTC+05:30", using the Unicode minus sign (U+2212)
    /// per the feature spec's example, not a plain hyphen.
    /// </summary>
    private static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "−" : "+";
        var magnitude = offset.Duration();
        return $"UTC{sign}{magnitude:hh\\:mm}";
    }
}
