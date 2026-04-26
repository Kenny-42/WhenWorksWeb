using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhenWorksWeb.Models
{
    /// <summary>
    /// Represents an event in the WhenWorks application. Events are created by users and can be interacted with by other users. 
    /// Each event has a title, a creator (optional), a creation timestamp, and a last active timestamp.
    /// </summary>
    public class Event
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // The title of the event, with a maximum length of 30 characters and minimum of 1 character.
        [StringLength(30, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 30 characters.")]
        public required string Title { get; set; }

        // The username of the account that created the event. Usernames are limited to 20 characters.
        // This field is nullable as not all events will be created by a logged in user.
        [StringLength(20)]
        public string? CreatedBy { get; set; }

        // The date and time when the event was created.
        public required DateTime CreatedAt { get; set; } = DateTime.Now;

        // The date and time when the event was last active. This field is updated whenever the event is modified or interacted with.
        public required DateTime LastActiveAt { get; set; } = DateTime.Now;
    }
}
