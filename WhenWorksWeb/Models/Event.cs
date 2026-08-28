using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models;

/// <summary>
/// Represents an event in the WhenWorks application. Events are created by users and can be interacted with by other users.
/// Each event has a title, a creator (optional), a creation timestamp, and a last active timestamp.
/// </summary>
public class Event
{
    /// <summary>The maximum length of <see cref="Title"/>.</summary>
    private const int TitleMaxLength = ModelConstants.EventTitleMaxLength;

    /// <summary>The maximum length of <see cref="CreatedByUserId"/>.</summary>
    private const int UserIdMaxLength = ModelConstants.UserIdMaxLength;

    /// <summary>The required length of <see cref="Code"/>.</summary>
    private const int CodeLength = ModelConstants.UniqueCodeLength;

    /// <summary>
    /// The database id for the event.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// A unique 6-character alphanumeric (excluding A,E,I,L,O,U,0,1) code used to identify and share the event.
    /// The code is case-insensitive (stored and compared without regard to letter case).
    /// </summary>
    [StringLength(CodeLength, MinimumLength = CodeLength, ErrorMessage = "Code must be exactly 6 characters.")]
    [RegularExpression(ModelConstants.EventCodePattern, ErrorMessage = "Code must be alphanumeric (excluding A,E,I,L,O,U,0,1).")]
    public required string Code { get; set; }

    /// <summary>
    /// The title of the event, with a maximum length of 30 characters and minimum of 1 character.
    /// </summary>
    [StringLength(TitleMaxLength, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 30 characters.")]
    public required string Title { get; set; }

    /// <summary>
    /// The username of the account that created the event. Usernames are limited to 450 characters.
    /// This field is nullable as not all events will be created by a logged in user.
    /// </summary>
    [StringLength(UserIdMaxLength)]
    public string? CreatedByUserId { get; set; }

    /// <summary>
    /// The date and time when the event was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; internal set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The date and time when the event was last active.
    /// </summary>
    public DateTimeOffset LastActiveAt { get; internal set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The user who created the event, if any.
    /// </summary>
    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>
    /// The participants who have joined the event.
    /// </summary>
    public ICollection<Participant> Participants { get; set; } = new List<Participant>();

    /// <summary>
    /// The candidate dates proposed for the event.
    /// </summary>
    public ICollection<EventDate> Dates { get; set; } = new List<EventDate>();

    /// <summary>
    /// The optional display settings (emoji, description) for the event.
    /// </summary>
    public EventSettings? Settings { get; set; }

    /// <summary>
    /// The organizer-chosen final date(s) for the event, set independently of participant
    /// availability. Each entry is either a single day or a date range.
    /// </summary>
    public ICollection<EventFinalDate> FinalDates { get; set; } = new List<EventFinalDate>();

    /// <summary>
    /// The chat messages posted within the event.
    /// </summary>
    public ICollection<EventMessage> Messages { get; set; } = new List<EventMessage>();

    /// <summary>
    /// The bookmarks users have saved on the event.
    /// </summary>
    public ICollection<UserEventBookmark> UserBookmarks { get; set; } = new List<UserEventBookmark>();

    /// <summary>
    /// Creates a new <see cref="Event"/> instance with the required properties. The CreatedAt and LastActiveAt
    /// timestamps are automatically set to the current time when the event is created.
    /// </summary>
    /// <param name="code">The unique event code.</param>
    /// <param name="title">The event title.</param>
    /// <param name="createdByUserId">The id of the user creating the event, or null for a guest-created event.</param>
    public static Event Create(string code, string title, string? createdByUserId = null)
    {
        return new Event
        {
            Code = code,
            Title = title,
            CreatedByUserId = createdByUserId
        };
    }
}

/// <summary>
/// Represents a specific date and time associated with an event, allowing for multiple potential occurrences of the
/// same event.
/// </summary>
public class EventDate
{
    /// <summary>
    /// The database id for the event date.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Event this date belongs to.
    /// </summary>
    public required int EventId { get; set; }

    /// <summary>
    /// The date and time of the event occurrence. This represents a potential date for the event,
    /// and multiple EventDate entries can exist for a single event to allow for scheduling flexibility.
    /// </summary>
    public required DateTimeOffset Date { get; set; }

    /// <summary>
    /// The event this date belongs to.
    /// </summary>
    public Event Event { get; set; } = null!;

    /// <summary>
    /// The participants who have marked themselves available on this date.
    /// </summary>
    public ICollection<ParticipantAvailability> Availabilities { get; set; } = new List<ParticipantAvailability>();
}

/// <summary>
/// Represents one participant's mark of availability on one candidate <see cref="EventDate"/>.
/// A join row between <see cref="Participant"/> and <see cref="EventDate"/> — its presence means
/// that participant is available on that date; there is no "unavailable" row, only absence.
/// </summary>
public class ParticipantAvailability
{
    /// <summary>
    /// Foreign key to the participant who marked themselves available. Together with
    /// <see cref="EventDateId"/>, this forms the composite primary key.
    /// </summary>
    public required int ParticipantId { get; set; }

    /// <summary>
    /// Foreign key to the candidate date the participant marked themselves available on.
    /// </summary>
    public required int EventDateId { get; set; }

    /// <summary>
    /// The participant who marked themselves available.
    /// </summary>
    public Participant Participant { get; set; } = null!;

    /// <summary>
    /// The candidate date this availability mark applies to.
    /// </summary>
    public EventDate EventDate { get; set; } = null!;
}

/// <summary>
/// Represents an organizer-chosen final date (or date range) for an event, set independently of
/// candidate <see cref="EventDate"/> entries and participant availability. A single-day final
/// date has a null <see cref="EndDate"/>.
/// </summary>
public class EventFinalDate
{
    /// <summary>
    /// The database id for the final date entry.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Event this final date belongs to.
    /// </summary>
    public required int EventId { get; set; }

    /// <summary>
    /// The first (or only, if <see cref="EndDate"/> is null) day of this final date entry.
    /// </summary>
    public required DateOnly StartDate { get; set; }

    /// <summary>
    /// The last day of this final date entry, for a date range. Null for a single-day entry.
    /// When set, must be on or after <see cref="StartDate"/> — enforced by the controller, not
    /// a database constraint.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// The event this final date belongs to.
    /// </summary>
    public Event Event { get; set; } = null!;
}

/// <summary>
/// Represents optional display settings for an event, such as its emoji and description.
/// </summary>
public class EventSettings
{
    /// <summary>The maximum length of <see cref="Emoji"/>.</summary>
    private const int EmojiMaxLength = ModelConstants.EventEmojiMaxLength;

    /// <summary>The maximum length of <see cref="Description"/>.</summary>
    private const int DescriptionMaxLength = ModelConstants.EventDescriptionMaxLength;

    /// <summary>
    /// Foreign key to the Event these settings belong to. This is also the primary key for this table,
    /// since each event can have at most one settings entry.
    /// </summary>
    [Key]
    public int EventId { get; set; }

    /// <summary>
    /// An emoji that represents the event, which can be used for display purposes.
    /// The maximum length is set to 20 characters to allow for a wide range of emojis while preventing excessively long strings.
    /// </summary>
    [StringLength(EmojiMaxLength)]
    public required string Emoji { get; set; }

    /// <summary>
    /// A description of the event, which can provide additional details or context. This field is optional.
    /// </summary>
    [StringLength(DescriptionMaxLength)]
    public string? Description { get; set; }

    /// <summary>
    /// The event these settings belong to.
    /// </summary>
    public Event Event { get; set; } = null!;
}
