// Covers ww-color-utils.js's pure color/geometry helpers (see
// FEATURES-vitest-js-test-coverage.ospec Step 5) -- one test file per source file, per this
// initiative's mirrored-path convention.
import { beforeAll, describe, expect, it } from 'vitest';
import { loadScript } from './helpers/loadScript.js';

let WWColorUtils;

beforeAll(() => {
    loadScript('ww-color-utils.js');
    WWColorUtils = window.WWColorUtils;
});

describe('hexToRgba', () => {
    it('converts a hex color and alpha to an rgba() string', () => {
        expect(WWColorUtils.hexToRgba('#ff6ec0', 0.62)).toBe('rgba(255, 110, 192, 0.62)');
    });

    it('supports zero alpha', () => {
        expect(WWColorUtils.hexToRgba('#000000', 0)).toBe('rgba(0, 0, 0, 0)');
    });
});

describe('rgbToHsl / hslToRgb', () => {
    it('round-trips a color through rgb -> hsl -> rgb', () => {
        const [h, s, l] = WWColorUtils.rgbToHsl(239, 73, 169);
        expect(WWColorUtils.hslToRgb(h, s, l)).toEqual([239, 73, 169]);
    });

    it('treats pure gray as zero saturation', () => {
        const [, s] = WWColorUtils.rgbToHsl(128, 128, 128);
        expect(s).toBe(0);
    });

    it('converts zero-saturation hsl back to a gray rgb triplet', () => {
        expect(WWColorUtils.hslToRgb(0, 0, 50)).toEqual([128, 128, 128]);
    });
});

describe('boostSaturation', () => {
    it('increases saturation of a soft/pastel color', () => {
        const [, sourceS] = WWColorUtils.rgbToHsl(239, 73, 169);
        const boosted = WWColorUtils.boostSaturation(239, 73, 169);
        const [, boostedS] = WWColorUtils.rgbToHsl(boosted[0], boosted[1], boosted[2]);

        expect(boostedS).toBeGreaterThan(sourceS);
    });

    it('caps saturation at 100', () => {
        const boosted = WWColorUtils.boostSaturation(255, 0, 0);
        const [, s] = WWColorUtils.rgbToHsl(boosted[0], boosted[1], boosted[2]);
        expect(s).toBeLessThanOrEqual(100);
    });
});

describe('distanceToRectEdge', () => {
    const rect = { left: 10, top: 10, right: 50, bottom: 30 };

    it('is zero for a point inside the rect', () => {
        expect(WWColorUtils.distanceToRectEdge(rect, 20, 20)).toBe(0);
    });

    it('measures straight-line distance to the nearest edge', () => {
        expect(WWColorUtils.distanceToRectEdge(rect, 30, 40)).toBe(10);
    });

    it('measures diagonal distance to the nearest corner', () => {
        expect(WWColorUtils.distanceToRectEdge(rect, 0, 0)).toBeCloseTo(Math.sqrt(200), 5);
    });
});

describe('smoothstep', () => {
    it('returns 0 at t=0 and 1 at t=1', () => {
        expect(WWColorUtils.smoothstep(0)).toBe(0);
        expect(WWColorUtils.smoothstep(1)).toBe(1);
    });

    it('returns 0.5 at the midpoint', () => {
        expect(WWColorUtils.smoothstep(0.5)).toBeCloseTo(0.5, 10);
    });

    it('eases rather than ramping linearly near the ends', () => {
        // 3t^2 - 2t^3 at t=0.1 is well below the linear value of 0.1.
        expect(WWColorUtils.smoothstep(0.1)).toBeLessThan(0.1);
    });
});
