using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models;

/// <summary>
/// POST target for <c>EventsController.Settings.cs</c>'s <c>UpdateDetails</c> action — the "Shape
/// the plan" form's title/description/emoji. Trimmed before validation by the action itself (the
/// DB check constraint on <see cref="Participant.DisplayName"/> requiring pre-trimmed values is a
/// separate field, but the same trim-before-validate approach is used here for consistency).
/// </summary>
public sealed class EventUpdateDetailsViewModel
{
    /// <summary>
    /// Gets or sets the event's title.
    /// </summary>
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(ModelConstants.EventTitleMaxLength, MinimumLength = 1,
        ErrorMessage = "Title must be between {2} and {1} characters.")]
    [RegularExpression(ModelConstants.DisplayNameContentPattern, ErrorMessage = "Title contains invalid characters.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event's description. Null/empty clears it.
    /// </summary>
    [StringLength(ModelConstants.EventDescriptionMaxLength,
        ErrorMessage = "Description must be {1} characters or fewer.")]
    [RegularExpression(ModelConstants.DescriptionContentPattern, ErrorMessage = "Description contains invalid characters.")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the event's emoji. Null/empty leaves the existing emoji unchanged — see
    /// <c>UpdateDetails</c>.
    /// </summary>
    [SingleGrapheme(ErrorMessage = "Emoji must be a single emoji character.")]
    public string? Emoji { get; set; }
}
