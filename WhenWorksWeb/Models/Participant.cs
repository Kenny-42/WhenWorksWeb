using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models;

/// <summary>
/// Represents a participant in an event. A participant can be either a registered user (linked to an ApplicationUser) or a guest (without a UserId).
/// </summary>
public class Participant
{
    /// <summary>The maximum length of <see cref="UserId"/>.</summary>
    private const int UserIdMaxLength = ModelConstants.UserIdMaxLength;

    /// <summary>The maximum length of <see cref="DisplayName"/>.</summary>
    private const int DisplayNameMaxLength = ModelConstants.ParticipantDisplayNameMaxLength;

    /// <summary>The regular expression <see cref="Color"/> must match.</summary>
    private const string HexColorPattern = ModelConstants.HexColorPattern;

    /// <summary>The required length of <see cref="Color"/>.</summary>
    private const int HexColorLength = ModelConstants.HexColorLength;

    /// <summary>The required length of <see cref="RejoinCode"/>.</summary>
    private const int RejoinCodeLength = ModelConstants.UniqueCodeLength;

    /// <summary>The regular expression <see cref="RejoinCode"/> must match.</summary>
    private const string RejoinCodePattern = ModelConstants.EventCodePattern;

    /// <summary>
    /// The database id for the participant.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Event this participant belongs to.
    /// </summary>
    public required int EventId { get; set; }

    /// <summary>
    /// Foreign key to the ApplicationUser if this participant is a registered user. Null for guest participants.
    /// </summary>
    [StringLength(UserIdMaxLength)]
    public string? UserId { get; set; }

    /// <summary>
    /// The display name of the participant for this event.
    /// This can be changed from the user's global display name and allows guests to have a name as well.
    /// </summary>
    [StringLength(DisplayNameMaxLength, MinimumLength = 1, ErrorMessage = "Display name must be between 1 and 16 characters.")]
    public required string DisplayName { get; set; }

    /// <summary>
    /// A hexadecimal color code (without the '#' symbol) that represents the participant's personal color for this event.
    /// This can be changed from the user's global color and allows guests to have a color as well.
    /// </summary>
    [RegularExpression(HexColorPattern, ErrorMessage = "Color must be a valid 6-character hexadecimal value.")]
    [StringLength(HexColorLength)]
    public required string Color { get; set; }

    /// <summary>
    /// A unique 6-character code that allows the participant to rejoin the same event later.
    /// This uses the same format and generation rules as event codes for consistency.
    /// </summary>
    [StringLength(RejoinCodeLength, MinimumLength = RejoinCodeLength, ErrorMessage = "Rejoin code must be exactly 6 characters.")]
    [RegularExpression(RejoinCodePattern, ErrorMessage = "Rejoin code must be alphanumeric (excluding A,E,I,L,O,U,0,1).")]
    public string? RejoinCode { get; set; }

    /// <summary>
    /// The event this participant belongs to.
    /// </summary>
    public Event Event { get; set; } = null!;

    /// <summary>
    /// The registered user this participant is linked to, if any.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// The role assigned to this participant within the event, if any.
    /// </summary>
    public EventRole? Role { get; set; }

    /// <summary>
    /// The chat messages sent by this participant.
    /// </summary>
    public ICollection<EventMessage> Messages { get; set; } = new List<EventMessage>();
}

/// <summary>
/// Represents the role assigned to a participant within a specific event.
/// </summary>
public class EventRole
{
    /// <summary>The maximum length of <see cref="Name"/>.</summary>
    private const int RoleNameMaxLength = ModelConstants.RoleNameMaxLength;

    /// <summary>
    /// References the ParticipantId as the primary key, since each participant can have at most one role in an event.
    /// </summary>
    [Key]
    public int ParticipantId { get; set; }

    /// <summary>
    /// The name of the role assigned to the participant for this event. This can be used for permissions or display purposes.
    /// </summary>
    [StringLength(RoleNameMaxLength, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 30 characters.")]
    public required string Name { get; set; }

    /// <summary>
    /// The participant this role is assigned to.
    /// </summary>
    public Participant Participant { get; set; } = null!;
}

/// <summary>
/// Represents a message sent by a participant within an event.
/// </summary>
public class EventMessage
{
    /// <summary>The maximum length of <see cref="Body"/>.</summary>
    private const int MessageBodyMaxLength = ModelConstants.MessageBodyMaxLength;

    /// <summary>The maximum length of <see cref="SenderDisplayName"/>.</summary>
    private const int SenderDisplayNameMaxLength = ModelConstants.ParticipantDisplayNameMaxLength;

    /// <summary>The regular expression <see cref="SenderColor"/> must match.</summary>
    private const string HexColorPattern = ModelConstants.HexColorPattern;

    /// <summary>The required length of <see cref="SenderColor"/>.</summary>
    private const int HexColorLength = ModelConstants.HexColorLength;

    /// <summary>
    /// The database id for the message.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Event this message belongs to.
    /// </summary>
    public required int EventId { get; set; }

    /// <summary>
    /// Foreign key to the Participant who sent this message.
    /// This is nullable so the participant can be deleted without losing chat history.
    /// </summary>
    public int? ParticipantId { get; set; }

    /// <summary>
    /// Snapshot of the sender display name at the time the message was created.
    /// This is used only as a fallback if the participant row is later deleted.
    /// </summary>
    [StringLength(SenderDisplayNameMaxLength, MinimumLength = 1, ErrorMessage = "Display name must be between 1 and 16 characters.")]
    public required string SenderDisplayName { get; set; }

    /// <summary>
    /// Snapshot of the sender color at the time the message was created.
    /// This is used only as a fallback if the participant row is later deleted.
    /// </summary>
    [RegularExpression(HexColorPattern, ErrorMessage = "Color must be a valid 6-character hexadecimal value.")]
    [StringLength(HexColorLength)]
    public required string SenderColor { get; set; }

    /// <summary>
    /// The content of the message, with a maximum length of 160 characters and minimum of 1 character.
    /// </summary>
    [StringLength(MessageBodyMaxLength, MinimumLength = 1, ErrorMessage = "Message body must be between 1 and 160 characters.")]
    public required string Body { get; set; }

    /// <summary>
    /// The date and time when the message was sent.
    /// </summary>
    public required DateTime SentAt { get; set; }

    /// <summary>
    /// The date and time when the message was last edited. Null if the message has never been edited.
    /// </summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>
    /// The event this message belongs to.
    /// </summary>
    public Event Event { get; set; } = null!;

    /// <summary>
    /// The participant who sent this message, if the participant record still exists.
    /// </summary>
    public Participant? Participant { get; set; }
}
