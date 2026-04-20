using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace WhenWorksWeb.Models
{
    /// <summary>
    /// Represents a custom application user with additional properties for display name, color, and activity tracking.
    /// </summary>
    /// <remarks> Use this class to store and manage user-specific data beyond the default identity fields. </remarks>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// Stores the user's preferred display name for use in events, which can be different from their username. 
        /// This allows users to have a more personalized and friendly name shown in the application, especially in event-related contexts. 
        /// The maximum length is set to 16 characters to ensure concise display names.
        /// </summary>
        [MaxLength(16)]
        public string DisplayName { get; set; }

        /// <summary>
        /// Stores a hexadecimal color code (without the '#' symbol) that represents the user's preferred personal color for use in events.
        /// </summary>
        [MaxLength(6)]
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the user account was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Stores the last time the user was active in the application, such as logging in or performing any action. 
        /// This can be used for features like showing online status or for analytics purposes.
        /// </summary>
        public DateTime LastActiveAt { get; set; }
    }
}
