using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models
{
    /// <summary>
    /// Represents an event in the WhenWorks application. Events are created by users and can be interacted with by other users. 
    /// Each event has a title, a creator (optional), a creation timestamp, and a last active timestamp.
    /// </summary>
    public class Event
    {
        private const int TitleMaxLength = ModelConstants.EventTitleMaxLength;
        private const int UserIdMaxLength = ModelConstants.UserIdMaxLength;
        private const int CodeLength = ModelConstants.EventCodeLength;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // A unique 6-character alphanumeric (excluding A,E,I,L,O,U,0,1) code used to identify and share the event.
        // The code is case-insensitive (stored and compared without regard to letter case).
        [StringLength(CodeLength, MinimumLength = CodeLength, ErrorMessage = "Code must be exactly 6 characters.")]
        [RegularExpression(ModelConstants.EventCodePattern, ErrorMessage = "Code must be alphanumeric (excluding A,E,I,L,O,U,0,1).")]
        public required string Code { get; set; }

        // The title of the event, with a maximum length of 30 characters and minimum of 1 character.
        [StringLength(TitleMaxLength, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 30 characters.")]
        public required string Title { get; set; }

        // The username of the account that created the event. Usernames are limited to 450 characters.
        // This field is nullable as not all events will be created by a logged in user.
        [StringLength(UserIdMaxLength)]
        public string? CreatedByUserId { get; set; }

        // The date and time when the event was created.
        public required DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // The date and time when the event was last active. This field is updated whenever the event is modified or interacted with.
        public required DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation
        public ApplicationUser? CreatedByUser { get; set; }
        public ICollection<Participant> Participants { get; set; } = new List<Participant>();
        public ICollection<EventDate> Dates { get; set; } = new List<EventDate>();
        public EventSettings? Settings { get; set; }
        public ICollection<EventMessage> Messages { get; set; } = new List<EventMessage>();
        public ICollection<UserEventBookmark> UserBookmarks { get; set; } = new List<UserEventBookmark>();
    }

    /// <summary>
    /// Represents a specific date and time associated with an event, allowing for multiple potential occurrences of the
    /// same event.
    /// </summary>
    public class EventDate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Foreign key to the Event this date belongs to
        public required int EventId { get; set; }

        // The date and time of the event occurrence. This represents a potential date for the event,
        // and multiple EventDate entries can exist for a single event to allow for scheduling flexibility.
        public required DateTimeOffset Date { get; set; }

        // Navigation
        public Event Event { get; set; } = null!;
    }

    public class EventSettings
    {
        private const int EmojiMaxLength = ModelConstants.EventEmojiMaxLength;
        private const int DescriptionMaxLength = ModelConstants.EventDescriptionMaxLength;

        // Foreign key to the Event these settings belong to. This is also the primary key for this table,
        // since each event can have at most one settings entry.
        [Key]
        public int EventId { get; set; }

        // An emoji that represents the event, which can be used for display purposes.
        // The maximum length is set to 20 characters to allow for a wide range of emojis while preventing excessively long strings.
        [StringLength(EmojiMaxLength)]
        public required string Emoji { get; set; } = ModelConstants.DefaultEventEmoji;

        // A description of the event, which can provide additional details or context. This field is optional.
        [StringLength(DescriptionMaxLength)]
        public string? Description { get; set; }

        // Navigation
        public Event Event { get; set; } = null!;
    }
}
