// Shared color/geometry helpers extracted out of site.js's canvas-blob and spotlight IIFEs
// (see FEATURES-vitest-js-test-coverage.ospec Step 5) so this pure math is reachable from a
// test instead of trapped inside no-export IIFEs. Framework-agnostic and DOM-free -- callers
// (site.js) still own all the animation/DOM wiring built on top of it.
(function (global) {
    "use strict";

    // #rrggbb -> "rgba(r, g, b, alpha)". Used by the background-blob canvas loop, which draws
    // gradients that need an alpha channel a plain hex string can't express.
    function hexToRgba(hex, alpha) {
        var r = parseInt(hex.slice(1, 3), 16);
        var g = parseInt(hex.slice(3, 5), 16);
        var b = parseInt(hex.slice(5, 7), 16);
        return "rgba(" + r + ", " + g + ", " + b + ", " + alpha + ")";
    }

    // rgb (0-255 each) -> hsl (h in degrees, s/l in percent). Standard conversion, used only by
    // boostSaturation below.
    function rgbToHsl(r, g, b) {
        r /= 255;
        g /= 255;
        b /= 255;
        var max = Math.max(r, g, b);
        var min = Math.min(r, g, b);
        var h = 0;
        var s = 0;
        var l = (max + min) / 2;

        if (max !== min) {
            var d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            switch (max) {
                case r: h = (g - b) / d + (g < b ? 6 : 0); break;
                case g: h = (b - r) / d + 2; break;
                default: h = (r - g) / d + 4; break;
            }
            h /= 6;
        }

        return [h * 360, s * 100, l * 100];
    }

    // hsl -> rgb, the inverse of rgbToHsl above.
    function hslToRgb(h, s, l) {
        h /= 360;
        s /= 100;
        l /= 100;

        if (s === 0) {
            var gray = Math.round(l * 255);
            return [gray, gray, gray];
        }

        var hue2rgb = function (p, q, t) {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1 / 6) return p + (q - p) * 6 * t;
            if (t < 1 / 2) return q;
            if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
            return p;
        };

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;

        return [
            Math.round(hue2rgb(p, q, h + 1 / 3) * 255),
            Math.round(hue2rgb(p, q, h) * 255),
            Math.round(hue2rgb(p, q, h - 1 / 3) * 255)
        ];
    }

    // Pushes a color noticeably more vivid than its source -- the feature cards' own accent
    // tokens are deliberately soft/pastel to fit the page, but the spotlight reusing that exact
    // color read as dull rather than glowing.
    function boostSaturation(r, g, b) {
        var hsl = rgbToHsl(r, g, b);
        var boostedS = Math.min(100, hsl[1] + 80);
        var vividL = Math.min(64, Math.max(48, hsl[2]));
        return hslToRgb(hsl[0], boostedS, vividL);
    }

    // Exact distance from a point to the nearest point ON a rect's boundary (0 if inside) --
    // clamping the point to the rect per axis, then measuring from that clamped point. Used
    // instead of a "distance to center minus half the larger dimension" approximation, which
    // under/over-counted the real gap for non-square elements (e.g. a wide, short capsule),
    // producing inconsistent glow between same-distance neighbors of different shapes.
    function distanceToRectEdge(rect, x, y) {
        var dx = Math.max(rect.left - x, 0, x - rect.right);
        var dy = Math.max(rect.top - y, 0, y - rect.bottom);
        return Math.sqrt(dx * dx + dy * dy);
    }

    // Smoothstep (3t² - 2t³) rather than a linear ramp for the fade between two distance
    // thresholds -- eases in/out at both ends instead of a constant rate.
    function smoothstep(t) {
        return t * t * (3 - 2 * t);
    }

    global.WWColorUtils = {
        hexToRgba: hexToRgba,
        rgbToHsl: rgbToHsl,
        hslToRgb: hslToRgb,
        boostSaturation: boostSaturation,
        distanceToRectEdge: distanceToRectEdge,
        smoothstep: smoothstep
    };
})(window);
