using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models;

/// <summary>
/// Represents a custom application user with additional properties for display name, color, and activity tracking.
/// </summary>
/// <remarks> Use this class to store and manage user-specific data beyond the default identity fields. </remarks>
public class ApplicationUser : IdentityUser
{
    /// <summary>The maximum length of <see cref="DisplayName"/>.</summary>
    private const int DisplayNameMaxLength = ModelConstants.ApplicationUserDisplayNameMaxLength;

    /// <summary>The regular expression <see cref="Color"/> must match.</summary>
    private const string HexColorPattern = ModelConstants.HexColorPattern;

    /// <summary>The required length of <see cref="Color"/>.</summary>
    private const int HexColorLength = ModelConstants.HexColorLength;

    /// <summary>The maximum length of a foreign key referencing this user's id.</summary>
    private const int UserIdMaxLength = ModelConstants.UserIdMaxLength;

    /// <summary>
    /// Stores the user's preferred display name for use in events, which can be different from their username.
    /// This allows users to have a more personalized and friendly name shown in the application, especially in event-related contexts.
    /// The maximum length is set to 16 characters to ensure concise display names.
    /// </summary>
    [StringLength(DisplayNameMaxLength)]
    public string DisplayName { get; set; } = "Nickname";

    /// <summary>
    /// Stores a hexadecimal color code (without the '#' symbol) that represents the user's preferred personal color for use in events.
    /// </summary>
    [RegularExpression(HexColorPattern, ErrorMessage = "Color must be a valid 6-character hexadecimal value.")]
    [StringLength(HexColorLength, MinimumLength = HexColorLength, ErrorMessage = "Color must be exactly 6 characters.")]
    public string Color { get; set; } = ModelConstants.DefaultParticipantColor;

    /// <summary>
    /// Gets or sets the date and time when the user account was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// Stores the last time the user was active in the application, such as logging in or performing any action.
    /// This can be used for features like showing online status or for analytics purposes.
    /// </summary>
    public required DateTime LastActiveAt { get; set; }

    /// <summary>
    /// The event participant records this user has created across all events.
    /// </summary>
    public ICollection<Participant> Participations { get; set; } = new List<Participant>();

    /// <summary>
    /// The events this user has bookmarked.
    /// </summary>
    public ICollection<UserEventBookmark> EventBookmarks { get; set; } = new List<UserEventBookmark>();

    /// <summary>
    /// The events this user has created.
    /// </summary>
    public ICollection<Event> CreatedEvents { get; set; } = new List<Event>();
}

/// <summary>
/// A class representing a bookmark that a user can set on an event. This allows users to mark events they are interested in for easy access later.
/// </summary>
public class UserEventBookmark
{
    /// <summary>The maximum length of <see cref="UserId"/>.</summary>
    private const int UserIdMaxLength = ModelConstants.UserIdMaxLength;

    /// <summary>
    /// Foreign key to the ApplicationUser who bookmarked the event.
    /// </summary>
    [StringLength(UserIdMaxLength)]
    public required string UserId { get; set; }

    /// <summary>
    /// Foreign key to the Event that is being bookmarked.
    /// </summary>
    public required int EventId { get; set; }

    /// <summary>
    /// The user who bookmarked the event.
    /// </summary>
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// The event that was bookmarked.
    /// </summary>
    public Event Event { get; set; } = null!;
}
