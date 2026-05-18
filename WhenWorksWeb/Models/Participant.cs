using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models
{
    /// <summary>
    /// Represents a participant in an event. A participant can be either a registered user (linked to an ApplicationUser) or a guest (without a UserId).
    /// </summary>
    public class Participant
    {
        private const int UserIdMaxLength = ModelConstants.UserIdMaxLength;
        private const int DisplayNameMaxLength = ModelConstants.ParticipantDisplayNameMaxLength;
        private const string HexColorPattern = ModelConstants.HexColorPattern;
        private const int HexColorLength = ModelConstants.HexColorLength;
        private const int RejoinCodeLength = ModelConstants.UniqueCodeLength;
        private const string RejoinCodePattern = ModelConstants.EventCodePattern;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Foreign key to the Event this participant belongs to
        public required int EventId { get; set; }

        // Foreign key to the ApplicationUser if this participant is a registered user. Null for guest participants.
        [StringLength(UserIdMaxLength)]
        public string? UserId { get; set; }

        // The display name of the participant for this event.
        // This can be changed from the user's global display name and allows guests to have a name as well.
        [StringLength(DisplayNameMaxLength, MinimumLength = 1, ErrorMessage = "Display name must be between 1 and 16 characters.")]
        public required string DisplayName { get; set; }

        // A hexadecimal color code (without the '#' symbol) that represents the participant's personal color for this event.
        // This can be changed from the user's global color and allows guests to have a color as well.
        [RegularExpression(HexColorPattern, ErrorMessage = "Color must be a valid 6-character hexadecimal value.")]
        [StringLength(HexColorLength)]
        public required string Color { get; set; }

        // A unique 6-character code that allows the participant to rejoin the same event later.
        // This uses the same format and generation rules as event codes for consistency.
        [StringLength(RejoinCodeLength, MinimumLength = RejoinCodeLength, ErrorMessage = "Rejoin code must be exactly 6 characters.")]
        [RegularExpression(RejoinCodePattern, ErrorMessage = "Rejoin code must be alphanumeric (excluding A,E,I,L,O,U,0,1).")]
        public string? RejoinCode { get; set; }

        // Navigation
        public Event Event { get; set; } = null!;
        public ApplicationUser? User { get; set; }
        public EventRole? Role { get; set; }
        public ICollection<EventMessage> Messages { get; set; } = new List<EventMessage>();
    }

    /// <summary>
    /// Represents the role assigned to a participant within a specific event.
    /// </summary>
    public class EventRole
    {
        private const int RoleNameMaxLength = ModelConstants.RoleNameMaxLength;

        // References the ParticipantId as the primary key, since each participant can have at most one role in an event.
        [Key]
        public int ParticipantId { get; set; }

        // The name of the role assigned to the participant for this event. This can be used for permissions or display purposes.
        [StringLength(RoleNameMaxLength, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 30 characters.")]
        public required string Name { get; set; }

        // Navigation
        public Participant Participant { get; set; } = null!;
    }

    /// <summary>
    /// Represents a message sent by a participant within an event.
    /// </summary>
    public class EventMessage
    {
        private const int MessageBodyMaxLength = ModelConstants.MessageBodyMaxLength;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Foreign key to the Event this message belongs to
        public required int EventId { get; set; }

        // Foreign key to the Participant who sent this message
        public required int ParticipantId { get; set; }

        // The content of the message, with a maximum length of 160 characters and minimum of 1 character.
        [StringLength(MessageBodyMaxLength, MinimumLength = 1, ErrorMessage = "Message body must be between 1 and 160 characters.")]
        public required string Body { get; set; }

        // The date and time when the message was sent.
        public required DateTime SentAt { get; set; } = DateTime.UtcNow;

        // The date and time when the message was last edited. Null if the message has never been edited.
        public DateTime? EditedAt { get; set; }

        // Navigation
        public Event Event { get; set; } = null!;
        public Participant Participant { get; set; } = null!;
    }
}
