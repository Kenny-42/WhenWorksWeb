// Single-emoji validation logic extracted out of site.js's jQuery Validate "grapheme" method
// (see FEATURES-vitest-js-test-coverage.ospec Step 5), mirroring
// Common/SingleGraphemeAttribute.cs's server-side check. Framework-agnostic and has no jQuery
// dependency -- site.js's own IIFE keeps the `if (!window.jQuery || ...) return;` page-detection
// guard and the `$.validator.addMethod`/`unobtrusive.adapters.addBool` registration itself,
// including the jQuery-Validate-specific "is this field optional and currently empty" check,
// calling into this module only for the pure single-grapheme check below.
(function (global) {
    "use strict";

    // Same control/zero-width rejection ModelConstants.DisplayNameContentPattern encodes
    // server-side, restated here in JS syntax (see that constant's own comment for why it's a
    // separate ASCII-escape-only pattern rather than shared verbatim with the server). Built
    // from a RegExp constructor (rather than a /.../ literal containing the raw characters) so
    // the zero-width codepoints stay as visible, greppable \u escapes in source instead of
    // invisible bytes an editor could silently mangle.
    var CONTROL_OR_ZERO_WIDTH_PATTERN = new RegExp("[\\x00-\\x1F\\x7F-\\x9F\\u200B\\u200C\\u200D\\u200E\\u200F\\uFEFF]");

    /// <summary>
    /// True if <c>value</c> is exactly one grapheme (matching SingleGraphemeAttribute
    /// server-side), false otherwise. Does not treat an empty/optional value as valid -- that's
    /// a jQuery-Validate-specific concern the caller (site.js) decides before calling in here.
    /// </summary>
    function isSingleGrapheme(value) {
        // Run against the ZWJ-stripped value, not the raw one -- matching
        // SingleGraphemeAttribute server-side: U+200D (zero-width joiner) is blocked as
        // invisible junk everywhere else, but it's also the actual joiner a compound emoji
        // sequence (e.g. the family emoji: man+ZWJ+woman+ZWJ+girl+ZWJ+boy) is built from, so a
        // legitimate ZWJ emoji must not fail this check just because the raw string contains
        // ZWJ. Stripping first still catches a value that's ZWJ characters and nothing else (the
        // stripped text is then empty, failing \S) while still allowing ZWJ as an interior
        // joiner between real content.
        var withoutJoiners = value.split(String.fromCharCode(0x200d)).join("");
        if (CONTROL_OR_ZERO_WIDTH_PATTERN.test(withoutJoiners) || !/\S/.test(withoutJoiners)) {
            return false;
        }

        var graphemeCount;
        if (global.Intl && global.Intl.Segmenter) {
            var segmenter = new global.Intl.Segmenter(undefined, { granularity: "grapheme" });
            graphemeCount = Array.from(segmenter.segment(value)).length;
        } else {
            // Fallback for a browser without Intl.Segmenter: counts Unicode code points
            // (correct for a plain single-codepoint emoji, but can undercount a
            // multi-codepoint sequence like a ZWJ family emoji as more than one grapheme). The
            // server-side check via StringInfo.GetTextElementEnumerator is the real, accurate
            // enforcement either way -- this is only a best-effort head start on showing the
            // error before submit.
            graphemeCount = Array.from(value).length;
        }

        return graphemeCount === 1;
    }

    global.WWGraphemeValidator = {
        isSingleGrapheme: isSingleGrapheme
    };
})(window);
