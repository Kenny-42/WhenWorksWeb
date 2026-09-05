using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace WhenWorksWeb.Models;

/// <summary>
/// POST target for <c>EventsController.FinalDate.cs</c>'s <c>AddFinalDate</c> action — the "Call
/// the date" card's add-date form. <see cref="StartDate"/>/<see cref="EndDate"/> stay strings
/// (posted by an <c>&lt;input type="date"&gt;</c> in <c>yyyy-MM-dd</c> format) rather than
/// <see cref="DateOnly"/> directly, since <see cref="DateOnly"/> model binding parses with the
/// current culture rather than a fixed format — the action still parses these with
/// <see cref="DateOnly.TryParseExact(string, string, IFormatProvider?, DateTimeStyles, out DateOnly)"/>
/// against the invariant culture, same as before this type existed, just with a friendly
/// <c>ModelState</c> error on failure instead of a bare 400.
/// </summary>
public sealed class EventAddFinalDateViewModel
{
    /// <summary>
    /// Gets or sets the first (or only) day of the final date entry, as <c>yyyy-MM-dd</c>.
    /// </summary>
    [Required(ErrorMessage = "Start date is required.")]
    public string StartDate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last day of the final date entry, as <c>yyyy-MM-dd</c>. Null/empty means a
    /// single-day entry.
    /// </summary>
    public string? EndDate { get; set; }

    /// <summary>
    /// The comma-separated set of <see cref="EventFinalDate"/> ids the client had at page-load —
    /// posted back so the action can detect (and reject) a submit against a final-dates list that
    /// changed server-side since then. See <c>EventsController.FinalDate.cs</c>'s
    /// <c>FinalDatesAreStaleAsync</c>.
    /// </summary>
    public string? KnownFinalDateIds { get; set; }
}
