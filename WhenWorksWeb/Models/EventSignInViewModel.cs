using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models;

/// <summary>
/// View model for the event sign-in page.
/// </summary>
public sealed class EventSignInViewModel
{
    /// <summary>
    /// Gets the code used to identify the event.
    /// </summary>
    /// <remarks>The code consists of exactly six alphanumeric characters, excluding the letters A, E, I,
    /// L, O, U and the digits 0 and 1.</remarks>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the name of the event associated with this instance.
    /// </summary>
    /// <remarks>The name has a maximum length of 30 characters and minimum of 1 character</remarks>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets or sets the display name for the participant.
    /// </summary>
    [Required]
    [StringLength(ModelConstants.ParticipantDisplayNameMaxLength, MinimumLength = 1,
        ErrorMessage = "Display name must be between 1 and 16 characters.")]
    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the color as a 6-character hexadecimal string.
    /// </summary>
    /// <remarks>The color value must be a valid hexadecimal code consisting of exactly six characters (0-9,
    /// A-F). This property is required and is commonly used to represent colors in web or UI contexts.</remarks>
    [Required]
    [RegularExpression(ModelConstants.HexColorPattern, ErrorMessage = "Color must be a valid 6-character hexadecimal value.")]
    [StringLength(ModelConstants.HexColorLength)]
    [Display(Name = "Color")]
    public string Color { get; set; } = "ff66c4";

    /// <summary>
    /// Gets or sets the display name selected from the list of existing display names.
    /// </summary>
    public string? SelectedExistingDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the list of available display names for selection in a user interface.
    /// </summary>
    public List<SelectListItem> ExistingDisplayName { get; set; } = [];
}
