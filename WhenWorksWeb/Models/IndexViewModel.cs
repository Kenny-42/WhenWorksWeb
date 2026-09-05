using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models;

/// <summary>
/// Represents the view model for the index page, providing properties for event creation and event code management.
/// </summary>
public sealed class IndexViewModel
{
    /// <summary>
    /// Gets or sets the name of the event to be created.
    /// </summary>
    [Required(ErrorMessage = "Event name is required.")]
    [StringLength(ModelConstants.EventTitleMaxLength, MinimumLength = 1, ErrorMessage = "Event name must be 30 characters or fewer.")]
    [RegularExpression(ModelConstants.DisplayNameContentPattern, ErrorMessage = "Event name contains invalid characters.")]
    public string? CreateEventName { get; set; }

    /// <summary>
    /// Gets or sets the event code associated with the current operation.
    /// </summary>
    [Required(ErrorMessage = "Event code is required.")]
    [StringLength(ModelConstants.UniqueCodeLength, MinimumLength = ModelConstants.UniqueCodeLength,
        ErrorMessage = "Event code must be exactly 6 characters.")]
    [RegularExpression(ModelConstants.EventCodePattern,
        ErrorMessage = "Event code must be alphanumeric (excluding A,E,I,L,O,U,0,1).")]
    public string? EventCode { get; set; }

    /// <summary>
    /// The organizer's browser-detected IANA timezone id (<c>Intl.DateTimeFormat().resolvedOptions().timeZone</c>),
    /// prefilled into a hidden field by <c>Views/Home/Index.cshtml</c>'s script before the Create
    /// form submits. Null for a quick "New Event" submit that has no JS detection step (My Events
    /// page, navbar), and for any value that turns out not to be a real timezone id — either way,
    /// <c>EventsController.Create</c> falls back to <see cref="ModelConstants.DefaultEventTimeZoneId"/>.
    /// Deliberately unvalidated here (no <see cref="RequiredAttribute"/>/<see cref="StringLengthAttribute"/>)
    /// since a missing/malformed value is an expected, silently-handled case, not a form error to
    /// surface to the user.
    /// </summary>
    public string? TimeZoneId { get; set; }
}
