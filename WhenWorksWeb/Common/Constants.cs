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

    // The minimum length required for a user's password. Mirrors IdentityConfiguration's
    // Password.RequiredLength -- kept as its own constant (rather than referencing IdentityOptions
    // at the page level) so Areas/Identity page models can express the same rule in a
    // [StringLength] attribute without a dependency on the Identity configuration type.
    public const int PasswordMinLength = 8;

    // The maximum length allowed for a user's password (a sanity upper bound, not a security
    // requirement).
    public const int PasswordMaxLength = 100;

    // Requires at least one lowercase letter, one uppercase letter, one digit, and one symbol
    // somewhere in the value, matching IdentityConfiguration's Password character-class
    // requirements (RequireLowercase/RequireUppercase/RequireDigit/RequireNonAlphanumeric).
    // Written with plain ASCII character classes only (no \p{} Unicode property escapes) so it
    // evaluates identically server-side (.NET regex) and client-side (browser RegExp, which
    // throws a SyntaxError on \p{} unless the "u" flag is set -- and jQuery Validate's
    // unobtrusive regex adapter doesn't set it).
    public const string PasswordComplexityPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$";

    // The regular expression pattern for validating phone numbers in E.164-style format: an
    // optional leading '+' followed by 7-15 digits and nothing else (no spaces, dashes,
    // parentheses, or letters).
    public const string PhoneNumberPattern = @"^\+?[0-9]{7,15}$";

    // Blocks C0/C1 control characters and common zero-width/invisible Unicode characters, while
    // still requiring at least one non-whitespace character somewhere in the value (closing the
    // gap where [Required] alone accepts an all-whitespace value -- see CODING_CONVENTIONS.md's
    // StringLength/RegularExpression gotcha). Leading/trailing whitespace is intentionally still
    // allowed by this pattern -- callers trim it before persisting rather than rejecting it
    // outright. Written with \x/\u escapes rather than \p{Cc} for the same client/server
    // regex-engine-compatibility reason as PasswordComplexityPattern above.
    public const string DisplayNameContentPattern = @"^(?!.*[\x00-\x1F\x7F-\x9F\u200B\u200C\u200D\u200E\u200F\uFEFF]).*\S.*$";
}
