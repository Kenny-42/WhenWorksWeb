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
    // The name of the event being created. Must be between 1 and 30 characters long and is required for event creation.
    // This property is used to capture the name of the event that users want to create.
    [Required(ErrorMessage = "Event name is required.")]
    [StringLength(30, MinimumLength = 1, ErrorMessage = "Event name must be between 1 and 30 characters.")]

    public string CreateEventName { get; set; } = string.Empty;

    // The code used to identify the event. Must be exactly six alphanumeric characters, excluding the letters A, E, I, L, O, U
    // and the digits 0 and 1. This property is typically used to validate and reference events within the application.
    [Required(ErrorMessage = "Event code is required.")]
    [StringLength(ModelConstants.EventCodeLength, MinimumLength = ModelConstants.EventCodeLength,
        ErrorMessage = "Event code must be exactly 6 characters.")]
    [RegularExpression(ModelConstants.EventCodePattern,
        ErrorMessage = "Event code must be alphanumeric (excluding A,E,I,L,O,U,0,1).")]

    public string EventCode { get; set; } = string.Empty;
}