using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WhenWorksWeb.Common;

/// <summary>
/// Validates that a string is exactly one extended grapheme cluster (one visible "character," by
/// the usual human notion of a character — including a multi-codepoint emoji sequence like a
/// skin-tone modifier or a ZWJ family emoji) and free of the same control/zero-width characters
/// <see cref="ModelConstants.DisplayNameContentPattern"/> rejects.
/// </summary>
/// <remarks>
/// Deliberately not a "real emoji" codepoint allowlist — see the feature spec's Event emoji field
/// section for why: .NET regex has no derived-property support for Extended_Pictographic, so an
/// allowlist would mean hand-maintaining codepoint ranges that go stale as Unicode adds new emoji
/// each year. This only closes the "not arbitrary multi-character text" gap. A null or empty value
/// is valid — the field is optional, matching how <see cref="RequiredAttribute"/> composes with
/// other attributes elsewhere in this codebase (see CODING_CONVENTIONS.md's StringLength gotcha).
/// </remarks>
public sealed class SingleGraphemeAttribute : ValidationAttribute, IClientModelValidator
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string text || text.Length == 0)
        {
            return ValidationResult.Success;
        }

        if (!IsSingleGrapheme(text))
        {
            return new ValidationResult(ErrorMessage ?? "Must be a single emoji character.", [validationContext.MemberName ?? string.Empty]);
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Internal so <c>ModelConstantsTests</c>-style unit tests can exercise the grapheme-counting
    /// logic directly, without going through <see cref="ValidationAttribute"/> plumbing.
    /// </summary>
    internal static bool IsSingleGrapheme(string text)
    {
        // Reject control/zero-width characters and require at least one non-whitespace
        // character — the same rule DisplayNameContentPattern enforces elsewhere, but run
        // against the ZWJ-stripped text rather than the raw value: U+200D (zero-width
        // joiner) is one of the characters that pattern blocks as invisible junk everywhere
        // else, but it's also the actual Unicode joiner a compound emoji sequence (e.g. the
        // family emoji: man+ZWJ+woman+ZWJ+girl+ZWJ+boy) is built from, so it can't be
        // blanket-rejected here. Stripping it first still catches a value that's ZWJ
        // characters and nothing else (the stripped text is then empty, failing \S) while
        // still allowing ZWJ as an interior joiner between real content.
        var withoutJoiners = text.Replace("\u200D", string.Empty);
        if (withoutJoiners.Length == 0 || !Regex.IsMatch(withoutJoiners, ModelConstants.DisplayNameContentPattern))
        {
            return false;
        }

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var elementCount = 0;
        while (enumerator.MoveNext())
        {
            elementCount++;
            if (elementCount > 1)
            {
                return false;
            }
        }

        return elementCount == 1;
    }

    /// <summary>
    /// Registers the "grapheme" unobtrusive-validation rule so the browser rejects a multi-character
    /// paste before the form is even submitted. The matching client-side rule is registered in
    /// <c>wwwroot/js/site.js</c> (via <c>$.validator.addMethod</c>/<c>adapters.addBool</c>), since
    /// grapheme-cluster counting has no built-in jQuery Validate rule to piggyback on.
    /// </summary>
    public void AddValidation(ClientModelValidationContext context)
    {
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-grapheme", ErrorMessage ?? "Must be a single emoji character.");
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
        {
            attributes.Add(key, value);
        }
    }
}
