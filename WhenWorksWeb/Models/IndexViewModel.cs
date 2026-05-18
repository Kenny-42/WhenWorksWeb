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
    [StringLength(30, MinimumLength = 1, ErrorMessage = "Event name must be 30 characters or fewer.")]
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
}