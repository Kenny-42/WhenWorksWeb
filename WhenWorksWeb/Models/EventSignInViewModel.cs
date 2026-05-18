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
    /// Gets or sets the display name selected from the list of existing participant names.
    /// </summary>
    /// <remarks>An empty value means the user is creating a new participant.</remarks>
    public string? SelectedExistingDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the participant rejoin code.
    /// </summary>
    /// <remarks>This value is hidden for signed-in users and shown only when a participant selection requires
    /// reauthentication or rejoin verification.</remarks>
    [StringLength(ModelConstants.UniqueCodeLength, MinimumLength = ModelConstants.UniqueCodeLength,
        ErrorMessage = "Rejoin code must be exactly 6 characters.")]
    [RegularExpression(ModelConstants.EventCodePattern,
        ErrorMessage = "Rejoin code must be alphanumeric (excluding A,E,I,L,O,U,0,1).")]
    [Display(Name = "Rejoin Code")]
    public string? RejoinCode { get; set; }

    /// <summary>
    /// Gets or sets a value that determines whether the rejoin code input should be shown to the user.
    /// </summary>
    public bool ShowRejoinCodeInput { get; set; }

    /// <summary>
    /// Gets or sets the list of available participant options for the event.
    /// </summary>
    public List<ParticipantSelectionViewModel> ExistingParticipants { get; set; } = [];
}

/// <summary>
/// Represents a participant option shown in the event sign-in dropdown.
/// </summary>
public sealed class ParticipantSelectionViewModel
{
    /// <summary>
    /// Gets or sets the participant display name.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the participant color.
    /// </summary>
    public required string Color { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this participant is already associated with the current signed-in
    /// account.
    /// </summary>
    public bool IsAssociatedWithCurrentUser { get; set; }
}
