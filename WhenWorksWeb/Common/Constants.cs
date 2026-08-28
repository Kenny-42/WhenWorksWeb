namespace WhenWorksWeb.Common;

/// <summary>
/// Centralized source of truth for field lengths, regex patterns, and alphabets used across models and validation attributes.
/// </summary>
public static class ModelConstants
{
    // The maximum length for the title of an event.
    public const int EventTitleMaxLength = 30;

    // The maximum length for a participant's display name within an event.
    public const int ParticipantDisplayNameMaxLength = 16;

    // The maximum length for an application user's display name.
    public const int ApplicationUserDisplayNameMaxLength = 16;

    // The maximum length for the name of a user role within an event.
    public const int RoleNameMaxLength = 30;

    // The maximum length of characters the user can input in their message when using the chat system within an event.
    public const int MessageBodyMaxLength = 160;

    // The maximum length for an emoji associated with an event.
    public const int EventEmojiMaxLength = 20;

    // The maximum length for the description of an event.
    public const int EventDescriptionMaxLength = 1000;

    // The maximum length for an application user's unique identifier (username).
    public const int UserIdMaxLength = 450;

    // The length of a hexadecimal color code (without the leading '#').
    public const int HexColorLength = 6;

    // The regular expression pattern for validating hexadecimal color codes (6 characters, case-insensitive).
    public const string HexColorPattern = @"^[A-Fa-f0-9]{6}$";

    // The default emoji used for events when no custom emoji is provided.
    public const string DefaultEventEmoji = "🎉";

    // The event name submitted by the quick "New Event" actions (My Events page, navbar)
    // that skip the Home page's name-entry form and create an event immediately.
    public const string DefaultEventTitle = "New Event";

    // The default color assigned to a user or participant when no custom color is provided.
    // Literal copy of --color-accent-pink-fill in wwwroot/css/site.css (that variable is the
    // single source of truth for this hue — it's the site's global "hot pink" accent used
    // for link hovers, checked checkboxes, etc.; this constant just mirrors it in C# code
    // wherever a default color value is needed server-side, e.g. new ApplicationUser/
    // EventSignInViewModel instances and the development data seeder). Update both together
    // if this ever changes.
    public const string DefaultParticipantColor = "ff66c4";

    // The length of the event code used to uniquely identify an event.
    public const int UniqueCodeLength = 6;

    // Shared source of truth for event codes.
    public const string UniqueCodeAlphabet = "BCDFGHJKMNPQRSTVWXYZ23456789";

    // The regular expression pattern for validating event codes (6 alphanumeric characters).
    public const string EventCodePattern = @"^(?i:[BCDFGHJKMNPQRSTVWXYZ23456789]{6})$";
}
