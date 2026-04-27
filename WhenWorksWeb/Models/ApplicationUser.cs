using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models
{
    /// <summary>
    /// Represents a custom application user with additional properties for display name, color, and activity tracking.
    /// </summary>
    /// <remarks> Use this class to store and manage user-specific data beyond the default identity fields. </remarks>
    public class ApplicationUser : IdentityUser
    {
        private const int DisplayNameMaxLength = ModelConstants.ApplicationUserDisplayNameMaxLength;
        private const string HexColorPattern = ModelConstants.HexColorPattern;
        private const int HexColorLength = ModelConstants.HexColorLength;
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
        [StringLength(HexColorLength)]
        public string Color { get; set; } = "ff66c4";

        /// <summary>
        /// Gets or sets the date and time when the user account was created.
        /// </summary>
        public required DateTime CreatedAt { get; set; }

        /// <summary>
        /// Stores the last time the user was active in the application, such as logging in or performing any action. 
        /// This can be used for features like showing online status or for analytics purposes.
        /// </summary>
        public required DateTime LastActiveAt { get; set; }

        // Navigation
        public ICollection<Participant> Participations { get; set; } = new List<Participant>();
        public ICollection<UserEventBookmark> EventBookmarks { get; set; } = new List<UserEventBookmark>();
        public ICollection<Event> CreatedEvents { get; set; } = new List<Event>();
    }

    /// <summary>
    /// A class representing a bookmark that a user can set on an event. This allows users to mark events they are interested in for easy access later.
    /// </summary>
    public class UserEventBookmark
    {
        private const int UserIdMaxLength = ModelConstants.UserIdMaxLength;

        // Foreign key to the ApplicationUser who bookmarked the event.
        [StringLength(UserIdMaxLength)]
        public required string UserId { get; set; }

        // Foreign key to the Event that is being bookmarked.
        public required int EventId { get; set; }

        // Navigation
        public ApplicationUser User { get; set; } = null!;
        public Event Event { get; set; } = null!;
    }
}
