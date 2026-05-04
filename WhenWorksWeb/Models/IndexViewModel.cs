using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models;

/// <summary>
/// Gets or sets the event code used to identify a specific event.
/// </summary>
/// <remarks>The event code must be exactly six alphanumeric characters, excluding the letters A, E, I, L, O, U
/// and the digits 0 and 1. This property is typically used to validate and reference events within the
/// application.</remarks>
public sealed class IndexViewModel
{
    [Required(ErrorMessage = "Event code is required.")]

    [StringLength(ModelConstants.EventCodeLength, MinimumLength = ModelConstants.EventCodeLength,
        ErrorMessage = "Event code must be exactly 6 characters.")]

    [RegularExpression(ModelConstants.EventCodePattern,
        ErrorMessage = "Event code must be alphanumeric (excluding A,E,I,L,O,U,0,1).")]

    public string EventCode { get; set; } = string.Empty;
}