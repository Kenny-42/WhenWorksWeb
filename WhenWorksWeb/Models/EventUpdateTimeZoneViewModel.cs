using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models;

/// <summary>
/// POST target for <c>EventsController.Availability.cs</c>'s <c>UpdateTimeZone</c> action: the
/// timezone picker's submitted IANA id. <see cref="Required"/>/<see cref="StringLength"/> alone
/// only reject an empty/oversized value -- whether the id actually names a real timezone is
/// checked separately against <see cref="Services.TimeZoneOptionsProvider.IsValidTimeZoneId"/>,
/// same as any other dropdown a request could bypass with an arbitrary value.
/// </summary>
public sealed class EventUpdateTimeZoneViewModel
{
    /// <summary>
    /// Gets or sets the submitted IANA timezone id.
    /// </summary>
    [Required(ErrorMessage = "Choose a timezone.")]
    [StringLength(ModelConstants.EventTimeZoneIdMaxLength, MinimumLength = 1,
        ErrorMessage = "Choose a timezone.")]
    public string TimeZoneId { get; set; } = string.Empty;
}
