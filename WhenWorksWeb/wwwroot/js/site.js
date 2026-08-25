// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Animated page-background "blobs" drawn on #ww-gradient-canvas
// (_Layout.cshtml). This replaces what was originally a CSS animation (see
// site.css's comment on #ww-gradient-canvas for the history) — CSS
// animations of the background repeatedly failed to actually repaint for a
// live viewer on this site's real target browsers, even when they measured
// as "working" in automated screenshot checks. Driving the repaint directly
// from a requestAnimationFrame loop instead of hoping the browser decides a
// CSS animation needs one sidesteps that class of problem entirely.
(function () {
  "use strict";

  var canvas = document.getElementById("ww-gradient-canvas");
  if (!canvas || !canvas.getContext) {
    // No canvas support (or the element's missing) — the static gradient
    // already set on <body> in site.css is the fallback look; nothing else
    // to do here.
    return;
  }
  var ctx = canvas.getContext("2d");
  if (!ctx) {
    return;
  }

  // Same five hues --gradient-page's own stops use in site.css (the three
  // "pure" Grainient reference colors, plus the two in-between tones that
  // blend them into each other on that static wash) — reused here as
  // separate JS copies rather than read via getComputedStyle at runtime,
  // since these are fixed, never-themed values with no behavioral reason
  // to round-trip through CSS.
  // Brightened together with site.css's own --gradient-color-1/-2/-3 and
  // --gradient-page stops (see their comments there) — the page read as a
  // little murky at the original, deeper values. Same five hues, just
  // lighter; these must stay in sync with that CSS block.
  var PINK = "#ff6ec0";
  var CORAL = "#ff8bbd";
  var PEACH = "#ffb0ad";
  var ORANGE = "#ffd08e";
  var YELLOW = "#ffec8a";

  // Each blob drifts on its own independent sine path (not a shared
  // to-and-fro like a two-keyframe CSS animation) — different speed/phase
  // per blob means they're never in sync, which is what reads as "warping"
  // as they merge into and separate from each other, rather than the whole
  // background sliding as one piece. Positions/radius are fractions of the
  // viewport, recomputed against its current size every frame, so the
  // layout stays correct across resizes without a separate resize handler
  // needing to recompute anything itself.
  //
  // Big and heavily overlapping on purpose (an earlier version shrank
  // these down because at large sizes all four original blobs covered
  // almost the whole viewport almost always, making the motion nearly
  // impossible to see) — CENTER_ALPHA below is what makes "big and
  // overlapping" still read as movement rather than a flat wash: every
  // blob is well short of fully opaque, so overlapping blobs visibly mix
  // into in-between colors instead of the top one just erasing whatever's
  // under it, which is what makes them read as one continuously warping
  // blend rather than solid pom-poms.
  // Speeds ~25% faster and radii ~15% smaller than an earlier pass, per
  // developer feedback once the blend/overlap look itself was right.
  var blobs = [
    { color: PINK, baseX: 0.12, baseY: 0.18, ampX: 0.16, ampY: 0.14, speed: 1 / 6800, phase: 0, radius: 0.53 },
    { color: YELLOW, baseX: 0.88, baseY: 0.12, ampX: 0.14, ampY: 0.16, speed: 1 / 8200, phase: 2.1, radius: 0.51 },
    { color: PINK, baseX: 0.82, baseY: 0.88, ampX: 0.14, ampY: 0.12, speed: 1 / 9700, phase: 4.4, radius: 0.55 },
    { color: YELLOW, baseX: 0.15, baseY: 0.85, ampX: 0.16, ampY: 0.13, speed: 1 / 7500, phase: 1.2, radius: 0.53 },
    { color: CORAL, baseX: 0.5, baseY: 0.08, ampX: 0.18, ampY: 0.1, speed: 1 / 6000, phase: 3.2, radius: 0.43 },
    { color: ORANGE, baseX: 0.08, baseY: 0.5, ampX: 0.1, ampY: 0.18, speed: 1 / 9400, phase: 5.6, radius: 0.43 },
    { color: PEACH, baseX: 0.92, baseY: 0.55, ampX: 0.1, ampY: 0.16, speed: 1 / 7100, phase: 0.8, radius: 0.43 },
    { color: ORANGE, baseX: 0.55, baseY: 0.92, ampX: 0.16, ampY: 0.1, speed: 1 / 10500, phase: 2.8, radius: 0.47 },
  ];

  // Peak opacity at each blob's own center — well under fully opaque (1),
  // which is what lets overlapping blobs blend into each other rather than
  // the top one simply overwriting whatever's beneath it. See the comment
  // on `blobs` above.
  var CENTER_ALPHA = 0.62;

  var cssWidth = 0;
  var cssHeight = 0;

  function resize() {
    var dpr = window.devicePixelRatio || 1;
    cssWidth = window.innerWidth;
    cssHeight = window.innerHeight;
    canvas.width = cssWidth * dpr;
    canvas.height = cssHeight * dpr;
    canvas.style.width = cssWidth + "px";
    canvas.style.height = cssHeight + "px";
    // Draw in CSS-pixel coordinates from here on; this scale accounts for
    // the backing-store size bump above so blobs stay crisp on high-DPI
    // screens instead of blurry.
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  function drawFrame(elapsedMs) {
    ctx.clearRect(0, 0, cssWidth, cssHeight);
    var maxDimension = Math.max(cssWidth, cssHeight);

    for (var i = 0; i < blobs.length; i++) {
      var blob = blobs[i];
      var angle = elapsedMs * blob.speed + blob.phase;
      var x = (blob.baseX + blob.ampX * Math.sin(angle)) * cssWidth;
      var y = (blob.baseY + blob.ampY * Math.cos(angle * 1.15)) * cssHeight;
      var radius = blob.radius * maxDimension;

      var gradient = ctx.createRadialGradient(x, y, 0, x, y, radius);
      // CENTER_ALPHA, not fully opaque — see its own comment above for why.
      gradient.addColorStop(0, hexToRgba(blob.color, CENTER_ALPHA));
      // Radial gradients can't fade straight to "transparent" from a color
      // and keep the hue through the fade (the browser interpolates
      // through transparent black) — an explicit transparent copy of the
      // same color keeps the fade from muddying toward gray at the edge.
      gradient.addColorStop(1, hexToRgba(blob.color, 0));
      ctx.fillStyle = gradient;
      ctx.fillRect(0, 0, cssWidth, cssHeight);
    }
  }

  function hexToRgba(hex, alpha) {
    var r = parseInt(hex.slice(1, 3), 16);
    var g = parseInt(hex.slice(3, 5), 16);
    var b = parseInt(hex.slice(5, 7), 16);
    return "rgba(" + r + ", " + g + ", " + b + ", " + alpha + ")";
  }

  var reduceMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
  var rafId = null;

  function loop(elapsedMs) {
    drawFrame(elapsedMs);
    rafId = window.requestAnimationFrame(loop);
  }

  function start() {
    if (rafId !== null) {
      return;
    }
    rafId = window.requestAnimationFrame(loop);
  }

  function stop() {
    if (rafId === null) {
      return;
    }
    window.cancelAnimationFrame(rafId);
    rafId = null;
    // Leave the blobs at a fixed resting frame (elapsedMs 0) rather than a
    // blank canvas — same "still shows the design, just not moving" intent
    // as every other prefers-reduced-motion opt-out in site.css.
    drawFrame(0);
  }

  function applyMotionPreference() {
    if (reduceMotionQuery.matches) {
      stop();
    } else {
      start();
    }
  }

  resize();
  applyMotionPreference();

  window.addEventListener("resize", resize);
  // Live update if the OS/browser setting changes while the page is open —
  // matches how the site's CSS-driven prefers-reduced-motion media queries
  // already behave, rather than only checking once at load.
  if (reduceMotionQuery.addEventListener) {
    reduceMotionQuery.addEventListener("change", applyMotionPreference);
  } else if (reduceMotionQuery.addListener) {
    // Safari < 14 only supports the older MediaQueryList listener API.
    reduceMotionQuery.addListener(applyMotionPreference);
  }
})();

// Ambient cursor-following spotlight (#ww-spotlight, site.css) plus the
// proximity border-color blend on every "pill container" — one combined
// IIFE (as this originally was) since both are driven off the same
// mousemove/mouseleave listeners. Two other approaches were tried in the
// spotlight's place and dropped, in order: a "gooey" cursor-shadow layer
// (never read as more than a faint smear once tuned not to look harsh),
// then a version scoped to only paint inside Home's feature cards (painted
// on TOP of the card's own text instead of underneath it — a fundamentally
// different, and worse, paint-order situation than this element's own
// below-all-content stacking trick achieves). This is the original
// approach, restored, with a stronger alpha/radius — see #ww-spotlight's
// own comment in site.css for the reasoning on both.
(function () {
  "use strict";

  // Fine-pointer devices only (mouse/trackpad) — a touchscreen has no
  // hovering cursor to track, so creating this element there would just
  // leave a stray glow parked wherever the last tap happened. Read once at
  // load (unlike reduceMotionQuery above, which needs to react live because
  // it drives a loop that's already running) since a pointer type doesn't
  // change mid-session the way an OS setting can.
  var pointerQuery = window.matchMedia("(hover: hover) and (pointer: fine)");
  var reduceMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
  if (!pointerQuery.matches || reduceMotionQuery.matches) {
    return;
  }

  var spotlight = document.createElement("div");
  spotlight.id = "ww-spotlight";
  spotlight.setAttribute("aria-hidden", "true");

  // Inserted right before #ww-page-content (not appended last) so it paints
  // above the background canvas but underneath all real page content — see
  // site.css's own comment on #ww-spotlight for why that specific stacking
  // position, combined with this site's already-translucent surfaces, is
  // what keeps text/buttons legible on top of the glow instead of the glow
  // sitting over everything.
  var pageContent = document.getElementById("ww-page-content");
  if (pageContent) {
    document.body.insertBefore(spotlight, pageContent);
  } else {
    document.body.appendChild(spotlight);
  }

  // Peach (#ff9e9b, --gradient-color-2 — literally the midpoint stop of
  // --gradient-page's own 5-stop gradient) — same initial-value site.css's
  // own @property --spotlight-r/-g/-b declarations use, restated here so
  // updateSpotlightColor can fall back to it explicitly for anything that
  // isn't a glow-enabled card/pill (i.e. the raw page background). See
  // #ww-spotlight's own comment in site.css for why this specific color and
  // not brand pink or yellow, both tried and rejected first. Three separate
  // numbers (not one "r, g, b" string) because site.css registers each
  // channel as its own @property so it can be smoothly transitioned — that
  // only works if each channel is set as its own distinct value, not
  // substituted in from one combined string.
  var DEFAULT_SPOTLIGHT_R = 255;
  var DEFAULT_SPOTLIGHT_G = 158;
  var DEFAULT_SPOTLIGHT_B = 155;
  var lastColorTarget = null;

  // rgb (each 0-255) -> hsl (h in degrees, s/l in percent). Standard
  // conversion, used only by boostSaturation below to push a card's own
  // (sometimes fairly muted/pastel) accent color to something more vivid for
  // the spotlight specifically — the cards themselves keep their original,
  // unboosted accent for their own border tint.
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

  // Pushes a color noticeably more vivid than its source — the feature
  // cards' own accent tokens (e.g. the yellow card's burnt-orange border) are
  // deliberately soft/pastel to fit the rest of the page, but the spotlight
  // reusing that exact color one-for-one read as dull rather than glowing.
  // Saturation/lightness targets are both bumped up from this function's
  // original values (+70/46-60) alongside site.css's own alpha/radius
  // increase — the same "make the glow on cards stronger" request applies
  // here too, not just to the gradient's own alpha.
  function boostSaturation(r, g, b) {
    var hsl = rgbToHsl(r, g, b);
    var boostedS = Math.min(100, hsl[1] + 80);
    var vividL = Math.min(64, Math.max(48, hsl[2]));
    return hslToRgb(hsl[0], boostedS, vividL);
  }

  function updateSpotlightColor(target) {
    // Recomputed only when the hovered element actually changes, not on
    // every mousemove — getComputedStyle is comparatively expensive to call
    // at mousemove frequency, and the color only ever needs to change when
    // the cursor crosses into a differently-colored element anyway.
    if (target === lastColorTarget) {
      return;
    }
    lastColorTarget = target;

    // --card-glow-rgb is the same custom property the feature cards' own
    // border tint reads (site.css's .ww-feature-card--yellow/--blue/
    // --green) — reusing it here rather than a second, spotlight-specific
    // property means any future element that opts into that property
    // automatically tints the spotlight too. CSS custom properties inherit
    // down the DOM by default, so reading it directly off the hovered
    // element already reflects whatever ancestor (e.g. the card itself)
    // actually set it, with no manual .closest() walk needed.
    var accentRgb = target instanceof Element
      ? window.getComputedStyle(target).getPropertyValue("--card-glow-rgb").trim()
      : "";
    var channels = accentRgb ? accentRgb.split(",").map(function (value) {
      return parseFloat(value);
    }) : [];

    var isValidRgbTriplet = channels.length === 3 && channels.every(function (value) {
      return !isNaN(value);
    });

    var rgb = isValidRgbTriplet
      ? boostSaturation(channels[0], channels[1], channels[2])
      : [DEFAULT_SPOTLIGHT_R, DEFAULT_SPOTLIGHT_G, DEFAULT_SPOTLIGHT_B];

    spotlight.style.setProperty("--spotlight-r", rgb[0]);
    spotlight.style.setProperty("--spotlight-g", rgb[1]);
    spotlight.style.setProperty("--spotlight-b", rgb[2]);
  }

  // Every "pill container" this glow applies to, across the shared layout
  // and Home page — the feature cards, the nav links/toggle, the hamburger
  // toggler, the hero badge, the brand's own sparkle badge (the home link
  // now that the separate Home nav pill is gone), the account dropdown
  // panel, and the Create/Join capsules. OUTERMOST CONTAINERS ONLY — never
  // the controls nested inside one: no .go-button or other button, no
  // .ww-pill-input, and no .dropdown-item rows (the account panel itself
  // carries the glow for those). site.css's own comment on the shared glow
  // block spells out that rule. Queried once — this set doesn't change
  // after the page loads — rather than re-querying the DOM on every
  // mousemove.
  var glowTargets = document.querySelectorAll(
    ".ww-feature-card, .navbar .nav-link, .navbar-toggler, .ww-hero-badge, .ww-brand-badge, .ww-pill-group, .account-dropdown-menu"
  );

  // How close the cursor needs to be to a glow target before it's considered
  // "under the spotlight" — proximity, not literal :hover, is what drives
  // --glow-intensity below, so a card/pill the spotlight is merely near
  // still lights up somewhat, the same way the reactbits.dev demo's own
  // GlobalSpotlight works across a whole grid of cards rather than one at a
  // time. Scaled to roughly match #ww-spotlight's own visible radius
  // (site.css: 460px diameter, shrunk from an earlier 620px) so "lit up"
  // tracks "visibly under the glow".
  var GLOW_PROXIMITY_RADIUS = 193;
  var GLOW_FULL_INTENSITY_DISTANCE = GLOW_PROXIMITY_RADIUS * 0.45;
  var GLOW_FADE_OUT_DISTANCE = GLOW_PROXIMITY_RADIUS * 0.9;

  // Exact Euclidean distance from a point to the nearest point ON a rect's
  // own boundary (0 if the point is inside it) — clamping the point to the
  // rect on each axis independently, then measuring from that clamped point,
  // is the standard closest-point-on-a-box formula. Used instead of the
  // "distance to center, minus half the larger dimension" approximation an
  // earlier version of this used, which was accurate only for elements
  // that are roughly square: for a wide, short capsule (e.g. the 32rem
  // Create pill), subtracting half of its WIDTH (the larger of the two
  // dimensions) from a distance measured to its center systematically
  // under-counted the real gap above/below it and over-counted the real gap
  // left/right of it — which is what produced the reported bug of one
  // element lighting up fully while a same-distance neighbor of a
  // different shape/size showed almost nothing. A real rect has no single
  // "radius" to subtract in the first place; measuring to its actual
  // boundary sidesteps the whole approximation.
  function distanceToRectEdge(rect, x, y) {
    var dx = Math.max(rect.left - x, 0, x - rect.right);
    var dy = Math.max(rect.top - y, 0, y - rect.bottom);
    return Math.sqrt(dx * dx + dy * dy);
  }

  // Smoothstep (3t² - 2t³) rather than a raw linear ramp for the fade
  // between GLOW_FULL_INTENSITY_DISTANCE and GLOW_FADE_OUT_DISTANCE — eases
  // in and out at both ends of the fade instead of changing at a constant
  // rate the whole way, which is what made the transition itself (as
  // opposed to the distance measurement above) read as a little mechanical.
  function smoothstep(t) {
    return t * t * (3 - 2 * t);
  }

  // Updates --glow-intensity on every glow target based on the given
  // viewport point (the cursor, or wherever hideSpotlight/clearGlowTargets
  // below wants everything reset to zero).
  function updateGlowTargets(clientX, clientY) {
    glowTargets.forEach(function (target) {
      var rect = target.getBoundingClientRect();
      var distanceToEdge = distanceToRectEdge(rect, clientX, clientY);

      var intensity = 0;
      if (distanceToEdge <= GLOW_FULL_INTENSITY_DISTANCE) {
        intensity = 1;
      } else if (distanceToEdge <= GLOW_FADE_OUT_DISTANCE) {
        var t = (GLOW_FADE_OUT_DISTANCE - distanceToEdge) / (GLOW_FADE_OUT_DISTANCE - GLOW_FULL_INTENSITY_DISTANCE);
        intensity = smoothstep(t);
      }

      target.style.setProperty("--glow-intensity", intensity.toString());
    });
  }

  // Zeroes every glow target's intensity — used when the cursor leaves the
  // document entirely, since at that point there's no meaningful cursor
  // position left to measure proximity from.
  function clearGlowTargets() {
    glowTargets.forEach(function (target) {
      target.style.setProperty("--glow-intensity", "0");
    });
  }

  var hideTimeoutId = null;

  // How many of Home/Index.cshtml's sparkle particles are currently alive
  // (spawned but not yet finished animating), reported via the 'ww:sparkle-
  // start'/'ww:sparkle-end' events dispatched below. The spotlight and the
  // sparkles are otherwise two independent effects (this file vs. that
  // page's own script), so a shared DOM event is the connection point
  // rather than one file reaching into the other's internals.
  var activeSparkleCount = 0;

  function showSpotlight() {
    spotlight.style.opacity = "1";
  }

  function hideSpotlight() {
    spotlight.style.opacity = "0";
  }

  // Only actually hides if no sparkles are still flying — called both from
  // the idle-mouse timeout below and whenever the last active sparkle
  // finishes, so the spotlight keeps showing for exactly as long as sparkles
  // are on screen even if the cursor itself has gone still in the meantime
  // (e.g. resting over a feature card while its particles keep spawning).
  function attemptHideSpotlight() {
    if (activeSparkleCount > 0) {
      return;
    }
    hideSpotlight();
  }

  function handleMouseMove(event) {
    // transform, not left/top — moving the glow only ever triggers
    // compositing this way, never layout.
    spotlight.style.transform = "translate(" + event.clientX + "px, " + event.clientY + "px)";
    showSpotlight();
    updateSpotlightColor(event.target);
    updateGlowTargets(event.clientX, event.clientY);

    // Fade out once the cursor sits still for a bit (e.g. resting over a
    // control) rather than staying lit indefinitely — reads as reactive to
    // actual movement instead of a fixed decal glued under the cursor.
    // Goes through attemptHideSpotlight, not hideSpotlight directly, so an
    // idle cursor still won't cut off a sparkle burst mid-flight.
    window.clearTimeout(hideTimeoutId);
    hideTimeoutId = window.setTimeout(attemptHideSpotlight, 1200);
  }

  document.addEventListener("mousemove", handleMouseMove);
  document.addEventListener("mouseleave", function () {
    hideSpotlight();
    clearGlowTargets();
  });

  document.addEventListener("ww:sparkle-start", function () {
    activeSparkleCount++;
    showSpotlight();
    window.clearTimeout(hideTimeoutId);
  });

  document.addEventListener("ww:sparkle-end", function () {
    activeSparkleCount = Math.max(0, activeSparkleCount - 1);
    if (activeSparkleCount === 0) {
      attemptHideSpotlight();
    }
  });
})();
