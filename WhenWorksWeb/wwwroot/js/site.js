// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Animated page-background "blobs" drawn on #ww-gradient-canvas (_Layout.cshtml).
// Replaces a CSS animation, which repeatedly failed to actually repaint for a live
// viewer on this site's real target browsers — a requestAnimationFrame loop drives its
// own repaints directly instead of depending on the browser to decide one is needed.
(function () {
  "use strict";

  var canvas = document.getElementById("ww-gradient-canvas");
  if (!canvas || !canvas.getContext) {
    // No canvas support (or the element's missing) — the static gradient already set on
    // <body> in site.css is the fallback; nothing else to do here.
    return;
  }
  var ctx = canvas.getContext("2d");
  if (!ctx) {
    return;
  }

  // Same five hues --gradient-page's own stops use in site.css — duplicated here as
  // plain JS constants rather than read via getComputedStyle, since these are fixed,
  // never-themed values. Must stay in sync with that CSS block if they ever change.
  var PINK = "#ff6ec0";
  var CORAL = "#ff8bbd";
  var PEACH = "#ffb0ad";
  var ORANGE = "#ffd08e";
  var YELLOW = "#ffec8a";

  // Each blob drifts on its own independent sine path (different speed/phase per blob)
  // so they're never in sync, which reads as "warping" as they merge and separate rather
  // than the whole background sliding as one piece. Positions/radius are fractions of the
  // viewport, recomputed against its current size every frame, so layout stays correct
  // across resizes with no separate resize handler needed.
  //
  // Big and heavily overlapping on purpose — CENTER_ALPHA below (well under fully
  // opaque) is what makes that still read as movement: overlapping blobs visibly mix
  // into in-between colors instead of the top one erasing whatever's under it.
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

  // Peak opacity at each blob's own center — see the comment on `blobs` above for why
  // this stays well under 1.
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
    // Draw in CSS-pixel coordinates from here on; this scale accounts for the backing-
    // store size bump above so blobs stay crisp on high-DPI screens.
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
      gradient.addColorStop(0, hexToRgba(blob.color, CENTER_ALPHA));
      // A radial gradient can't fade straight to "transparent" from a color and keep the
      // hue through the fade (the browser interpolates through transparent black) — an
      // explicit transparent copy of the same color avoids muddying toward gray.
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
    // Leave the blobs at a fixed resting frame rather than a blank canvas — same "still
    // shows the design, just not moving" intent as every other reduced-motion opt-out.
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
  // Live update if the OS/browser setting changes while the page is open.
  if (reduceMotionQuery.addEventListener) {
    reduceMotionQuery.addEventListener("change", applyMotionPreference);
  } else if (reduceMotionQuery.addListener) {
    // Safari < 14 only supports the older MediaQueryList listener API.
    reduceMotionQuery.addListener(applyMotionPreference);
  }
})();

// Ambient cursor-following spotlight (#ww-spotlight, site.css) plus the proximity
// border-color blend on every "pill container" — one IIFE since both are driven off the
// same mousemove/mouseleave listeners. See #ww-spotlight's own comment in site.css for
// the visual reasoning; this is the original spotlight mechanism (a couple of
// alternatives were tried and dropped per developer feedback).
(function () {
  "use strict";

  // Fine-pointer devices only (mouse/trackpad) — a touchscreen has no hovering cursor to
  // track, so this element would just leave a stray glow parked at the last tap. Read
  // once at load since pointer type doesn't change mid-session.
  var pointerQuery = window.matchMedia("(hover: hover) and (pointer: fine)");
  var reduceMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
  if (!pointerQuery.matches || reduceMotionQuery.matches) {
    return;
  }

  var spotlight = document.createElement("div");
  spotlight.id = "ww-spotlight";
  spotlight.setAttribute("aria-hidden", "true");

  // Inserted right before #ww-page-content (not appended last) so it paints above the
  // background canvas but underneath all real page content — see site.css's comment on
  // #ww-spotlight for why that stacking position, combined with this site's translucent
  // surfaces, keeps text/buttons legible on top of the glow.
  var pageContent = document.getElementById("ww-page-content");
  if (pageContent) {
    document.body.insertBefore(spotlight, pageContent);
  } else {
    document.body.appendChild(spotlight);
  }

  // Peach (#ff9e9b, --gradient-color-2 — the midpoint stop of --gradient-page's own
  // gradient) — matches the @property initial-value in site.css, restated here so
  // updateSpotlightColor can fall back to it for anything with no --card-glow-rgb of its
  // own. Three separate numbers (not one string) since site.css registers each channel
  // as its own @property to make it smoothly transitionable.
  var DEFAULT_SPOTLIGHT_R = 255;
  var DEFAULT_SPOTLIGHT_G = 158;
  var DEFAULT_SPOTLIGHT_B = 155;
  var lastColorTarget = null;

  // rgb (0-255 each) -> hsl (h in degrees, s/l in percent). Standard conversion, used
  // only by boostSaturation below.
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

  // Pushes a color noticeably more vivid than its source — the feature cards' own accent
  // tokens are deliberately soft/pastel to fit the page, but the spotlight reusing that
  // exact color read as dull rather than glowing.
  function boostSaturation(r, g, b) {
    var hsl = rgbToHsl(r, g, b);
    var boostedS = Math.min(100, hsl[1] + 80);
    var vividL = Math.min(64, Math.max(48, hsl[2]));
    return hslToRgb(hsl[0], boostedS, vividL);
  }

  function updateSpotlightColor(target) {
    // Recomputed only when the hovered element changes, not on every mousemove —
    // getComputedStyle is comparatively expensive at mousemove frequency.
    if (target === lastColorTarget) {
      return;
    }
    lastColorTarget = target;

    // --card-glow-rgb is the same custom property the feature cards' border tint reads
    // (site.css) — reusing it means any future element that opts into that property
    // automatically tints the spotlight too. It inherits down the DOM, so reading it
    // directly off the hovered element already reflects whatever ancestor set it.
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

  // The live proximity-glow border is reserved for elements that also get Home's
  // card-sparkle confetti (.ww-feature-card), plus one deliberate exception: the navbar's
  // sparkle-icon brand badge (.ww-brand-badge) — everything else that still *looks* like
  // it has a glow-capable border (nav links, the hamburger toggler, the hero badge, the
  // Create/Join capsules, the account dropdown, the Sign-In/account cards, the Manage
  // nav links) keeps its --glow-intensity permanently at the CSS default of 0 (see
  // site.css's shared glow block), which resolves to a plain static resting border —
  // this NodeList is what actually drives the live blend, so simply not including an
  // element here is enough to keep its border static without touching its own CSS rule.
  // Queried once since this set doesn't change after load.
  var glowTargets = document.querySelectorAll(".ww-feature-card, .ww-brand-badge");

  // How close the cursor needs to be to a glow target before it's "under the spotlight" —
  // proximity, not literal :hover, drives --glow-intensity below, so a nearby card/pill
  // still lights up somewhat. Scaled to roughly match #ww-spotlight's own visible radius.
  var GLOW_PROXIMITY_RADIUS = 193;
  var GLOW_FULL_INTENSITY_DISTANCE = GLOW_PROXIMITY_RADIUS * 0.45;
  var GLOW_FADE_OUT_DISTANCE = GLOW_PROXIMITY_RADIUS * 0.9;

  // Exact distance from a point to the nearest point ON a rect's boundary (0 if inside) —
  // clamping the point to the rect per axis, then measuring from that clamped point.
  // Used instead of a "distance to center minus half the larger dimension" approximation,
  // which under/over-counted the real gap for non-square elements (e.g. a wide, short
  // capsule), producing inconsistent glow between same-distance neighbors of different
  // shapes.
  function distanceToRectEdge(rect, x, y) {
    var dx = Math.max(rect.left - x, 0, x - rect.right);
    var dy = Math.max(rect.top - y, 0, y - rect.bottom);
    return Math.sqrt(dx * dx + dy * dy);
  }

  // Smoothstep (3t² - 2t³) rather than a linear ramp for the fade between the two
  // distance thresholds — eases in/out at both ends instead of a constant rate.
  function smoothstep(t) {
    return t * t * (3 - 2 * t);
  }

  // Updates --glow-intensity on every glow target based on the given viewport point.
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

  // Zeroes every glow target's intensity — used when the cursor leaves the document.
  function clearGlowTargets() {
    glowTargets.forEach(function (target) {
      target.style.setProperty("--glow-intensity", "0");
    });
  }

  var hideTimeoutId = null;

  // How many of Home's sparkle particles are currently alive, reported via the
  // 'ww:sparkle-start'/'ww:sparkle-end' events dispatched below — a shared DOM event is
  // the connection point between these two otherwise-independent effects.
  var activeSparkleCount = 0;

  function showSpotlight() {
    spotlight.style.opacity = "1";
  }

  function hideSpotlight() {
    spotlight.style.opacity = "0";
  }

  // Only actually hides if no sparkles are still flying, so the spotlight keeps showing
  // for as long as sparkles are on screen even if the cursor itself has gone still.
  function attemptHideSpotlight() {
    if (activeSparkleCount > 0) {
      return;
    }
    hideSpotlight();
  }

  function handleMouseMove(event) {
    // transform, not left/top — moving the glow only ever triggers compositing, never
    // layout.
    spotlight.style.transform = "translate(" + event.clientX + "px, " + event.clientY + "px)";
    showSpotlight();
    updateSpotlightColor(event.target);
    updateGlowTargets(event.clientX, event.clientY);

    // Fade out once the cursor sits still for a bit, rather than staying lit
    // indefinitely — via attemptHideSpotlight (not hideSpotlight directly) so an idle
    // cursor still won't cut off a sparkle burst mid-flight.
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

// Confetti particles spawned from the cursor while a Home page feature card is hovered
// (Magic Bento-inspired — see the comment atop site.css's card-glow-plus-spotlight
// section for the full context). The proximity border-glow and page-wide spotlight
// above are shared-layout behavior; this IIFE only owns the part specific to Home:
// emitting confetti from the cursor while it's over a card.
(function () {
  "use strict";

  var cards = document.querySelectorAll(".ww-feature-card");
  if (cards.length === 0) {
    return;
  }

  // Skip for touch devices (no hovering cursor) and prefers-reduced-motion — this is a
  // pure decorative flourish, never required to use the page.
  var pointerQuery = window.matchMedia("(hover: hover) and (pointer: fine)");
  var reduceMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
  if (!pointerQuery.matches || reduceMotionQuery.matches) {
    return;
  }

  var SPARKLE_INTERVAL_MS = 65;
  // Spawned together each tick rather than only shortening the interval, so cards read as
  // busy with tiny particles without pushing the timer frequency high enough to add
  // real overhead.
  var SPARKLES_PER_TICK = 4;

  // Max burst distance (px) a sparkle can fly from its origin — used to randomize
  // --sparkle-dx/-dy and to map distance traveled to a smaller end size.
  var BURST_DISTANCE_PX = 140;

  // Radius (px) each particle's spawn point is randomized within around the cursor
  // (instead of landing on top of it), so a tick's worth of particles spreads out
  // instead of piling up on one pixel.
  var START_SPREAD_RADIUS_PX = 26;

  // Spawns one confetti particle at the given card-relative origin, colored to match
  // that card's own --card-glow-rgb accent. Announces 'ww:sparkle-start'/'ww:sparkle-end'
  // (which the spotlight IIFE above listens for) so the page-wide spotlight stays
  // visible for as long as sparkles are flying. Removed once its animation finishes.
  function spawnSparkle(card, sparkleHost, originX, originY) {
    var glowRgb = getComputedStyle(card).getPropertyValue("--card-glow-rgb").trim() || "239, 73, 169";

    // Random direction + distance (a burst, not a straight random walk); the origin
    // itself is jittered too (see START_SPREAD_RADIUS_PX) so a tick's particles don't
    // spawn stacked on the same pixel.
    var angle = Math.random() * Math.PI * 2;
    var distance = BURST_DISTANCE_PX * (0.35 + Math.random() * 0.65);
    var dx = Math.cos(angle) * distance;
    var dy = Math.sin(angle) * distance;

    var startAngle = Math.random() * Math.PI * 2;
    var startRadius = Math.random() * START_SPREAD_RADIUS_PX;

    var sparkle = document.createElement("div");
    sparkle.className = "ww-sparkle";
    sparkle.style.left = (originX + Math.cos(startAngle) * startRadius) + "px";
    sparkle.style.top = (originY + Math.sin(startAngle) * startRadius) + "px";
    // Flat-colored rectangle (confetti flake, not a glowing dot). Size randomized per
    // particle so the burst reads as a mix of little squares and slivers.
    var flakeWidth = 3 + Math.round(Math.random() * 3);
    var flakeHeight = 3 + Math.round(Math.random() * 3);
    sparkle.style.width = flakeWidth + "px";
    sparkle.style.height = flakeHeight + "px";
    sparkle.style.backgroundColor = "rgb(" + glowRgb + ")";
    sparkle.style.setProperty("--sparkle-dx", dx + "px");
    sparkle.style.setProperty("--sparkle-dy", dy + "px");
    // Full rotations (not a slight wobble) so it reads as genuinely tumbling; sign
    // randomized so flakes don't all spin the same way.
    var spin = (360 + Math.random() * 540) * (Math.random() < 0.5 ? -1 : 1);
    sparkle.style.setProperty("--sparkle-rot", spin.toFixed(0) + "deg");
    // Particles that fly further end up smaller — distance maps linearly onto the
    // 0.55-0.2 end-scale range.
    sparkle.style.setProperty("--sparkle-end-scale", (0.55 - (distance / BURST_DISTANCE_PX) * 0.35).toFixed(2));

    document.dispatchEvent(new CustomEvent("ww:sparkle-start"));
    sparkle.addEventListener("animationend", function () {
      sparkle.remove();
      document.dispatchEvent(new CustomEvent("ww:sparkle-end"));
    });

    // Into the card's clipping layer, not the card itself, so particles stay inside its
    // rounded bounds.
    sparkleHost.appendChild(sparkle);
  }

  cards.forEach(function (card) {
    var sparkleIntervalId = null;

    // Per-card clipping layer particles are appended into (site.css's
    // .ww-feature-card-sparkles). Created here rather than in markup since nothing
    // needs it if this script never runs (touch devices, reduced motion, JS disabled).
    var sparkleHost = document.createElement("div");
    sparkleHost.className = "ww-feature-card-sparkles";
    sparkleHost.setAttribute("aria-hidden", "true");
    card.appendChild(sparkleHost);

    // Listeners bind to this stable wrapper (the Bootstrap grid column,
    // .ww-feature-card-slot), not to `card` itself — `card` is what site.css's hover
    // rule lifts up on :hover, and binding directly to it caused a feedback loop: a
    // cursor resting near the card's bottom edge got un-hovered the instant the lift
    // carried that edge past it, then re-hovered once it dropped back, repeating for as
    // long as the cursor stayed put and firing a sparkle burst on every re-entry (the
    // reported flicker). The wrapper's box is unaffected by its child's transform, so it
    // can't suffer that loop. Falls back to `card` itself if the slot class is missing.
    var hoverZone = card.closest(".ww-feature-card-slot") || card;

    // Card-relative cursor position, kept current by mousemove and used as the sparkle
    // spawn origin — starts centered in case mouseenter fires before any mousemove.
    var cursorX = card.clientWidth / 2;
    var cursorY = card.clientHeight / 2;

    hoverZone.addEventListener("mousemove", function (event) {
      var rect = card.getBoundingClientRect();
      cursorX = event.clientX - rect.left;
      cursorY = event.clientY - rect.top;
    });

    // Sparkles spawn on an interval only while actively hovered, not one-shot on entry,
    // so lingering over a card keeps producing them.
    hoverZone.addEventListener("mouseenter", function (event) {
      var rect = card.getBoundingClientRect();
      cursorX = event.clientX - rect.left;
      cursorY = event.clientY - rect.top;

      for (var i = 0; i < SPARKLES_PER_TICK; i++) {
        spawnSparkle(card, sparkleHost, cursorX, cursorY);
      }
      sparkleIntervalId = window.setInterval(function () {
        for (var i = 0; i < SPARKLES_PER_TICK; i++) {
          spawnSparkle(card, sparkleHost, cursorX, cursorY);
        }
      }, SPARKLE_INTERVAL_MS);
    });

    hoverZone.addEventListener("mouseleave", function () {
      window.clearInterval(sparkleIntervalId);
      sparkleIntervalId = null;
    });
  });
})();

// Shrinks any element carrying the .ww-autofit-title class (event titles on the My
// Events cards, the Sign-In card heading, and the Event Home page heading) down toward a
// floor font size until its content fits the element's own box on one line. These are
// all user-supplied event titles, which can be a single long word with no natural break
// point — CSS alone has no way to detect "this exact string overflows this exact box"
// and shrink in response, only to wrap at existing spaces. Re-measures on resize
// (debounced) and once webfonts finish loading, since both change the rendered width
// this depends on.
(function () {
  "use strict";

  var MIN_FONT_SIZE_PX = 12;
  // Binary search converges to within this many px of the largest fitting size in a fixed
  // ~10-12 iterations regardless of how far the starting size is from the floor — unlike a
  // fixed step count counting down 1px at a time, which (previously, at up to 60 steps) could
  // run out before actually reaching a fitting size for a large enough starting size/floor gap
  // (e.g. the Event Home title's 120px-down-to-32px range), leaving the element stuck too big
  // and overflowing — the reported case where a long title briefly rendered three lines with
  // the third sliced off by the two-line cap's overflow: hidden instead of shrinking to fit.
  var SEARCH_TOLERANCE_PX = 0.5;
  var MAX_SEARCH_STEPS = 30;

  var targets = document.querySelectorAll(".ww-autofit-title");
  if (targets.length === 0) {
    return;
  }

  // For an element capped at a maximum number of lines (data-max-lines, e.g. the Event Home
  // title's two-line cap — most elements carrying this class have no such cap and skip this
  // half of the check entirely), "does it fit" is decided by literally counting the text's
  // actual rendered lines (via countLines below), not by estimating a height budget from
  // line-height. Two earlier versions tried the height-budget route and both broke: an
  // em-based max-height slack scaled with this element's own live-shrinking font-size, so at
  // large sizes it was generous enough for a real third line to sneak through; moving that
  // slack into padding instead grew clientHeight and scrollHeight in lockstep (both are
  // padding-box measurements under this app's global border-box sizing) so it never actually
  // bought any room, permanently rejecting an exactly-two-line title as "too tall" no matter
  // how far it shrank; and even a from-scratch line-height x max-lines estimate is still only
  // an estimate — .ww-hero-title's deliberately tight line-height (0.95, less than 1) means
  // real glyph ascenders/descenders routinely render taller than that nominal box, so a height
  // budget derived from it under-counts the room actually needed and forces the search to keep
  // shrinking below where the text has already reached two lines. Counting real lines side-
  // steps all of that: it's exactly the same signal the browser's own line-breaking used to
  // lay the text out, so it can't drift out of sync with tight line-height, font metrics, or
  // this element's own font-size changes across the search below. A separate, deliberately
  // generous CSS max-height on the element still exists as a visual safety-net clip only (see
  // .ww-event-title-row .ww-hero-title in site.css) — not what this function decides fit from.
  function countLines(el) {
    var range = document.createRange();
    range.selectNodeContents(el);
    var rects = range.getClientRects();

    var lineTops = [];
    for (var i = 0; i < rects.length; i++) {
      var top = rects[i].top;
      // A single visual line can still produce more than one DOMRect (rendering quirks, or a
      // rect per inline fragment) — dedupe by vertical position rather than counting rects
      // directly, with a small tolerance for the sub-pixel differences that can show up between
      // fragments that are really on the same line.
      var isNewLine = true;
      for (var j = 0; j < lineTops.length; j++) {
        if (Math.abs(lineTops[j] - top) <= 1) {
          isNewLine = false;
          break;
        }
      }
      if (isNewLine) {
        lineTops.push(top);
      }
    }

    return lineTops.length;
  }

  function fits(el) {
    if (el.scrollWidth > el.clientWidth) {
      return false;
    }

    var maxLines = parseInt(el.dataset.maxLines, 10);
    if (!maxLines) {
      return el.scrollHeight <= el.clientHeight;
    }

    return countLines(el) <= maxLines;
  }

  function fit(el) {
    // Reset to the CSS-authored size first — otherwise a box that's grown back (window
    // resized wider) would only ever shrink further from wherever it last stopped, never
    // recover.
    el.style.fontSize = "";
    var maxFontSize = parseFloat(window.getComputedStyle(el).fontSize);

    if (fits(el)) {
      return;
    }

    // Per-element floor via data-min-font-size, e.g. the Event Home title (see
    // _EventHeader.cshtml) — it should stay reading as a big heading even once shrunk,
    // never all the way down to the shared 12px floor other (much smaller) titles use.
    var minFontSize = parseFloat(el.dataset.minFontSize) || MIN_FONT_SIZE_PX;

    // lo is the largest size confirmed to fit so far (starts at the floor, whether or not the
    // floor itself actually fits — if it doesn't, that's the best this element can do and lo
    // stays the correct answer to converge on). hi is always confirmed *not* to fit (starts at
    // the CSS-authored size, already known not to fit from the check above).
    var lo = minFontSize;
    var hi = maxFontSize;

    for (var i = 0; i < MAX_SEARCH_STEPS && hi - lo > SEARCH_TOLERANCE_PX; i++) {
      var mid = (lo + hi) / 2;
      el.style.fontSize = mid + "px";
      if (fits(el)) {
        lo = mid;
      } else {
        hi = mid;
      }
    }

    el.style.fontSize = lo + "px";
  }

  // Ratio of the title's own resolved font size an adjacent emoji (e.g. .ww-event-title-emoji)
  // should render at, so it visibly scales with a title that can autofit anywhere from ~20px to
  // 144px instead of sitting at one fixed size that reads as tiny next to a big title.
  var EMOJI_SCALE_RATIO = 0.5;

  // If `el` carries data-scale-emoji-selector, looks that selector up from el's own parent (not
  // el itself — the emoji is a sibling, e.g. .ww-event-title-row > .ww-event-title-emoji + h1)
  // and sets every match's font-size to a fixed ratio of el's just-settled resolved size. Matches
  // *every* element the selector finds, not just the first — the Event Home title row carries
  // two: the real, visible emoji to the title's left and an identical but invisible mirror-image
  // copy after it, which keeps the title itself centered on the page (flexbox centers the
  // [emoji, title, invisible-emoji] group as a whole, so the title lands in the middle only if
  // the invisible copy matches the real one's rendered width exactly). No-op for targets without
  // the attribute (every .ww-autofit-title use besides the Event Home title).
  function scaleAdjacentEmoji(el) {
    var selector = el.dataset.scaleEmojiSelector;
    if (!selector || !el.parentElement) {
      return;
    }

    var emojiEls = el.parentElement.querySelectorAll(selector);
    if (emojiEls.length === 0) {
      return;
    }

    var resolvedFontSize = parseFloat(window.getComputedStyle(el).fontSize);
    emojiEls.forEach(function (emojiEl) {
      emojiEl.style.fontSize = (resolvedFontSize * EMOJI_SCALE_RATIO) + "px";
    });
  }

  function fitAll() {
    targets.forEach(function (el) {
      fit(el);
      scaleAdjacentEmoji(el);
    });
  }

  fitAll();

  if (document.fonts && document.fonts.ready) {
    document.fonts.ready.then(fitAll);
  }

  var resizeTimeoutId = null;
  window.addEventListener("resize", function () {
    window.clearTimeout(resizeTimeoutId);
    resizeTimeoutId = window.setTimeout(fitAll, 120);
  });
})();

// Every circular color-swatch picker (Event Sign-In, Register, Manage account) shares
// this one class, but each page wires its own "sync visible picker -> hidden posted
// field" script separately (they post to different hidden field ids) — this IIFE only
// owns the part that's identical everywhere: tinting the swatch's own outline to a
// darker shade of whatever color is currently picked, via the same
// color-mix(in srgb, <color> 80%, black 20%) formula the "My Profile" nav link uses for
// its own --user-color border (site.css).
(function () {
  "use strict";

  document.querySelectorAll(".color-swatch-picker").forEach(function (picker) {
    function updateBorderColor() {
      picker.style.borderColor = "color-mix(in srgb, " + picker.value + " 80%, black 20%)";
    }

    updateBorderColor();
    picker.addEventListener("input", updateBorderColor);
  });
})();

// Registers the "grapheme" jQuery Validate rule backing SingleGraphemeAttribute
// (Common/SingleGraphemeAttribute.cs) — the event emoji field. Grapheme-cluster counting
// has no built-in jQuery Validate rule to piggyback on (unlike [Required]/[StringLength]/
// [RegularExpression], which the unobtrusive library already knows how to render), so both
// halves (the rule itself and the adapter that reads data-val-grapheme off the input) are
// registered by hand here. A no-op if jQuery Validate isn't loaded on the current page —
// only pages with @Html.AntiForgeryToken()-style forms plus _ValidationScriptsPartial pull
// it in.
(function () {
  "use strict";

  if (!window.jQuery || !window.jQuery.validator || !window.jQuery.validator.unobtrusive) {
    return;
  }

  var $ = window.jQuery;

  $.validator.addMethod("grapheme", function (value, element) {
    if (this.optional(element)) {
      // Matches SingleGraphemeAttribute server-side: an empty value is valid, the field is
      // optional.
      return true;
    }

    // Same control/zero-width rejection ModelConstants.DisplayNameContentPattern encodes
    // server-side, restated here in JS syntax (see that constant's own comment for why it's
    // a separate ASCII-escape-only pattern rather than shared verbatim with the server).
    // Built from a RegExp constructor (rather than a /.../ literal containing the raw
    // characters) so the zero-width codepoints stay as visible, greppable \u escapes in
    // source instead of invisible bytes an editor could silently mangle.
    //
    // Run against the ZWJ-stripped value, not the raw one -- matching SingleGraphemeAttribute
    // server-side: U+200D (zero-width joiner) is blocked as invisible junk everywhere else,
    // but it's also the actual joiner a compound emoji sequence (e.g. the family emoji:
    // man+ZWJ+woman+ZWJ+girl+ZWJ+boy) is built from, so a legitimate ZWJ emoji must not fail
    // this check just because the raw string contains ZWJ. Stripping first still catches a
    // value that's ZWJ characters and nothing else (the stripped text is then empty, failing
    // \S) while still allowing ZWJ as an interior joiner between real content.
    var controlOrZeroWidthPattern = new RegExp("[\\x00-\\x1F\\x7F-\\x9F\\u200B\\u200C\\u200D\\u200E\\u200F\\uFEFF]");
    var withoutJoiners = value.split(String.fromCharCode(0x200d)).join("");
    if (controlOrZeroWidthPattern.test(withoutJoiners) || !/\S/.test(withoutJoiners)) {
      return false;
    }

    var graphemeCount;
    if (window.Intl && window.Intl.Segmenter) {
      var segmenter = new window.Intl.Segmenter(undefined, { granularity: "grapheme" });
      graphemeCount = Array.from(segmenter.segment(value)).length;
    } else {
      // Fallback for a browser without Intl.Segmenter: counts Unicode code points (correct
      // for a plain single-codepoint emoji, but can undercount a multi-codepoint sequence
      // like a ZWJ family emoji as more than one grapheme). The server-side check via
      // StringInfo.GetTextElementEnumerator is the real, accurate enforcement either way —
      // this is only a best-effort head start on showing the error before submit.
      graphemeCount = Array.from(value).length;
    }

    return graphemeCount === 1;
  }, "Must be a single emoji character.");

  $.validator.unobtrusive.adapters.addBool("grapheme");
})();

// Generic "copy this text to the clipboard" button, driven entirely by data attributes so any
// page can opt in without its own script -- currently just ShowRecoveryCodes.cshtml's "COPY
// CODES" button, which copies the freshly generated 2FA recovery codes as one newline-separated
// block so a user can paste the whole set into a password manager instead of transcribing them
// by hand.
(function () {
  "use strict";

  document.querySelectorAll("[data-copy-target]").forEach(function (button) {
    var target = document.querySelector(button.getAttribute("data-copy-target"));
    var label = button.querySelector(".ww-shiny-text");
    if (!target || !label) {
      return;
    }

    var idleText = button.getAttribute("data-copy-label") || label.textContent;
    var copiedText = button.getAttribute("data-copied-label") || "COPIED!";
    var resetTimeoutId = null;

    button.addEventListener("click", function () {
      // One code per line, matching how they're meant to be stored/pasted elsewhere -- not the
      // grid's visual left-to-right, top-to-bottom reading order.
      var codes = Array.from(target.querySelectorAll("code"))
        .map(function (codeEl) { return codeEl.textContent.trim(); })
        .join("\n");

      var showCopied = function () {
        window.clearTimeout(resetTimeoutId);
        label.textContent = copiedText;
        resetTimeoutId = window.setTimeout(function () {
          label.textContent = idleText;
        }, 2000);
      };

      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(codes).then(showCopied, function () {
          // Clipboard permission denied/unavailable -- leave the button's label alone rather
          // than falsely claiming success.
        });
        return;
      }

      // Fallback for a browser without the async Clipboard API: a hidden, offscreen textarea
      // plus the older synchronous execCommand copy.
      var textarea = document.createElement("textarea");
      textarea.value = codes;
      textarea.style.position = "fixed";
      textarea.style.opacity = "0";
      document.body.appendChild(textarea);
      textarea.focus();
      textarea.select();
      try {
        if (document.execCommand("copy")) {
          showCopied();
        }
      } catch (err) {
        // Copy unsupported -- leave the button's label alone rather than falsely claiming
        // success.
      }
      document.body.removeChild(textarea);
    });
  });
})();
