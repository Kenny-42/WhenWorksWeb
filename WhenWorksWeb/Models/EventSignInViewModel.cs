using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models;

/// <summary>
/// View model for the event sign-in page.
/// </summary>
/// <remarks>This type is both the MVC model binder's POST target for the sign-in form and a display model
/// built once by BuildSignInViewModelAsync. That means every property must tolerate being constructed by the
/// model binder with no corresponding form field — including EventName and ExistingParticipants, which have
/// no form input at all. Neither of those two can be `required`/non-nullable-without-a-default: the model
/// binder bypasses the compiler's `required` enforcement (it isn't built via `new { ... }` syntax), so a
/// `required` property with no default is left null after binding, which trips ASP.NET Core's automatic
/// non-nullable-reference-type validation and silently fails the whole POST. DisplayName, Color, and
/// SelectedExistingDisplayName are also reassigned in place by NormalizeSignInModel/ApplySignInViewModelState
/// after binding, so they stay mutable regardless.</remarks>
public sealed class EventSignInViewModel
{
    /// <summary>
    /// Gets the code used to identify the event.
    /// </summary>
    /// <remarks>The code consists of exactly six alphanumeric characters, excluding the letters A, E, I,
    /// L, O, U and the digits 0 and 1. This is posted via a hidden form field, so it's safe to require.</remarks>
    public required string Code { get; init; }

    /// <summary>
    /// Gets or sets the name of the event associated with this instance.
    /// </summary>
    /// <remarks>This is populated by the server for display only and is not validated as posted form input.</remarks>
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for the participant.
    /// </summary>
    [Required]
    [StringLength(ModelConstants.ParticipantDisplayNameMaxLength, MinimumLength = 1,
        ErrorMessage = "Display name must be between 1 and 16 characters.")]
    [RegularExpression(ModelConstants.DisplayNameContentPattern, ErrorMessage = "Display name contains invalid characters.")]
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
    public string Color { get; set; } = ModelConstants.DefaultParticipantColor;

    /// <summary>
    /// Gets or sets the display name selected from the list of existing participant names.
    /// </summary>
    /// <remarks>An empty value means the user is creating a new participant.</remarks>
    public string? SelectedExistingDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the list of available participant options for the event.
    /// </summary>
    public IReadOnlyList<ParticipantSelectionViewModel> ExistingParticipants { get; set; } = [];
}

/// <summary>
/// Represents a participant option shown in the event sign-in dropdown.
/// </summary>
public sealed class ParticipantSelectionViewModel
{
    /// <summary>
    /// Gets or sets the participant display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the participant color.
    /// </summary>
    public required string Color { get; init; }
}
