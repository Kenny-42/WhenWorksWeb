// Covers ww-grapheme-validator.js's pure single-emoji check (see
// FEATURES-vitest-js-test-coverage.ospec Step 5) -- one test file per source file, per this
// initiative's mirrored-path convention.
//
// isSingleGrapheme's optional-field short-circuit ("is this field empty and not required?")
// is jQuery-Validate-specific and lives in site.js's own IIFE, not in this module -- so that
// decision isn't tested here. isSingleGrapheme itself still rejects an empty string on its own
// terms (zero graphemes, not one), which the empty-string test below covers directly.
import { beforeAll, describe, expect, it } from 'vitest';
import { loadScript } from './helpers/loadScript.js';

let WWGraphemeValidator;

beforeAll(() => {
    loadScript('ww-grapheme-validator.js');
    WWGraphemeValidator = window.WWGraphemeValidator;
});

describe('isSingleGrapheme', () => {
    it('accepts a single-codepoint emoji', () => {
        expect(WWGraphemeValidator.isSingleGrapheme('🎉')).toBe(true);
    });

    it('accepts a ZWJ-joined compound emoji as one grapheme', () => {
        // Family: man + ZWJ + woman + ZWJ + girl + ZWJ + boy.
        const family = '\u{1F468}‍\u{1F469}‍\u{1F467}‍\u{1F466}';
        expect(WWGraphemeValidator.isSingleGrapheme(family)).toBe(true);
    });

    it('rejects two separate emoji', () => {
        expect(WWGraphemeValidator.isSingleGrapheme('🎉🎊')).toBe(false);
    });

    it('rejects plain ascii text', () => {
        expect(WWGraphemeValidator.isSingleGrapheme('ab')).toBe(false);
    });

    it('accepts a single ascii letter (grapheme count only, no emoji-ness check)', () => {
        expect(WWGraphemeValidator.isSingleGrapheme('a')).toBe(true);
    });

    it('rejects a value containing a control character', () => {
        expect(WWGraphemeValidator.isSingleGrapheme('')).toBe(false);
    });

    it('rejects an empty string', () => {
        expect(WWGraphemeValidator.isSingleGrapheme('')).toBe(false);
    });

    it('rejects a value that is only zero-width characters', () => {
        expect(WWGraphemeValidator.isSingleGrapheme('​')).toBe(false);
    });

    it('rejects a value that is only ZWJ characters once stripped', () => {
        expect(WWGraphemeValidator.isSingleGrapheme('‍‍')).toBe(false);
    });
});
