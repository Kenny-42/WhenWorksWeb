# WhenWorks Style Guide

This document explains the *intent* behind the site's visual style — the "why," not the "what." For the actual color/shape/font values, see the `:root` custom properties block at the top of `wwwroot/css/site.css` — that file is the single source of truth for values, so nothing here should need updating just because a value changes.

## Vibe
Soft, bubbly, bright, happy, airy. Driven by reference mockups (a warm animated gradient background, pill-shaped controls with a "3D pop" bottom-edge shadow, soft white-glass panels, a rounded display font, two-color headings) that this file's token set is deliberately styled to match closely — including matching the mockups' own use of white text directly on the open gradient, which the site does **not** try to certify as WCAG-compliant (see Accessibility Pass below).

## Tokens

### `--gradient-page`
The site's signature background — a five-stop animated diagonal gradient (hot pink → coral → salmon → light orange → pale gold), applied at the `body` level so it's consistent across every page. Drifts slowly via `background-position` animation, disabled under `prefers-reduced-motion`.

### `--color-accent-yellow-fill` / `--color-accent-yellow-border`
Fill/border pair for the "primary" input pill (e.g. Create Event) and the account dropdown (its toggle and the panel it opens). Keep yellow associated with the primary/creating action as new controls are added.

### `--color-accent-blue-fill` / `--color-accent-blue-border`
Fill/border pair for the "secondary" input pill (e.g. Join Event) and the My Events nav link. Keep blue associated with the secondary/joining action.

It doubles as the fallback fill for navbar controls with no action-color of their own — the Login pill and the hamburger toggler. Both were white-glass originally and read as barely-there against the pale gradient; blue is the quietest accent that still registers as a control, and neither of them competes with a joining action for the meaning (Login is never on screen at the same time as My Events).

### `--color-accent-green`
Reserved for the "GO!" pattern (`.go-button`) and the nav's one primary CTA (Register) — a bright pop against the gradient. Use sparingly; it loses its "go"/primary prominence if applied broadly to ordinary buttons.

### `--color-accent-red` / `--color-accent-red-border`
Destructive actions (`.modal-delete-button`, the Logout dropdown item's hover state).

### `--color-text-on-gradient`
White. Used directly on the open gradient for large/bold headings (`.ww-hero-title`, nav, footer) — matching the reference mockups exactly rather than routing everything through a compliant backdrop. See Accessibility Pass below for why this changed from an earlier, stricter version of this token.

### `--color-text-strong`
A dark, dusty gray (`#514c54`) — the default dark text color on light surfaces: accent fills (yellow/blue/green pills, GO button interiors), white-glass tags/cards (`.ww-hero-badge`, `.ww-feature-card`), and the `.text-on-gradient` utility. An earlier version of this token was a thick dark plum; that read fine at regular body weight but looked heavy/off on bold headings, so it's desaturated to a plain dusty gray instead — works at both weights.

### `--color-accent-brand-glow` (`#dfff5c`)
The bright yellow-green accent from the reference mockups — the hero heading's second line (`.ww-hero-title-accent`) and the brand wordmark's trailing period (`.ww-brand-dot`).

### `--radius-pill`
Everything interactive (nav links, buttons, inputs) is a full pill, not a softly-rounded rectangle — a deliberate signature of the "bubbly" vibe.

### `--shadow-hard` / `--shadow-hard-hover` / `--shadow-hard-active` / `--shadow-hard-focus`
The site's one shadow language: a solid offset block with no blur, in one neutral dark plum (`--shadow-hard-color`) regardless of the element's own accent — the reference mockups' "3D pop" bottom edge. Every interactive control uses one of these four (never a bespoke blurred/tinted shadow of its own), and the states express lift by height: taller on hover, shorter while pressed, tallest of all (`-focus`) for a container whose child input is actively focused (e.g. `.ww-pill-group:focus-within` — the Event Name/Event Code capsules bounce rather than drawing an outline on the input itself). Each state is paired with a matching `translateY` on the same rule.

This is unrelated to `#ww-spotlight` (`site.js`/`site.css`) — the original ambient, page-wide, colored cursor glow (screen/plus-lighter blended, tracking the cursor across the whole shared layout, tinted to whatever's under it via each element's `--card-glow-rgb`). Two replacements for it were tried and dropped: a "gooey" cursor-shadow layer built from blurred, merging blobs, and a version scoped to paint only inside Home's feature cards. Both were reverted per explicit developer direction — the page-wide spotlight is the intended, permanent design, not a placeholder. Alpha and diameter were bumped up from that original version (0.3/0.15/0.05 at 520px → 0.48/0.26/0.1 at 620px) since the old per-card box-shadow bloom, which used to add its own extra visible ring on top of this same spotlight, is gone; `boostSaturation` in `site.js` was bumped to match. The three feature cards read as "specified"/more vivid than the plain page background not through any card-specific code, but because they carry their own `--card-glow-rgb`, which this same spotlight always reads from whatever's under the cursor.

### `--font-display` (Fredoka)
Reserved for short, large text: headings, button labels. Not legible at small sizes or in long-form text — never use it for body copy, form labels, or validation messages.

### `--font-body` (Nunito)
The default readable typeface for everything else — labels, paragraphs, validation messages, general UI text.

### `--font-mono` (DM Mono)
Small "eyebrow"/code-like text only — currently the Join event-code input. Not a body font.

## White-Glass vs. Dark Text
Two backdrop-free surface styles recur across the page, and which one an element uses decides its text color:
- **Open gradient, no backdrop** (hero heading/subtitle, nav, footer) — white text (`--color-text-on-gradient`), matching the reference mockups directly.
- **White-glass** (`.ww-hero-badge`, `.ww-feature-card`, the account dropdown's own rows, nav pills with no accent of their own) — a translucent white fill (roughly 55–85% opacity + blur) with dark dusty-gray text (`--color-text-strong`). This is the default for any new small tag/card/panel — it needs no special reasoning, it's just dark-on-light. Note it works as a *surface* far better than as a *control*: white-glass buttons sitting directly on the gradient (Login, the hamburger toggler) were hard to pick out and now take an accent fill instead.

Ordinary body content elsewhere (paragraphs, labels, form controls, tables) stays Bootstrap's normal dark text by default.

## Current Scope
These tokens are applied to the shared layout (navbar/footer), Home, Events/SignIn, MyEvents/Index, and the Identity account pages — though Home's mockup-matched treatment (white text directly on the gradient, per-link nav colors, white-glass tags/cards) is the newest pass and hasn't been retrofitted onto SignIn/MyEvents/Identity yet; those still use the prior `--color-text-strong`-on-bare-gradient pattern. Events/Home (the post-join event landing page) hasn't been styled at all yet. Don't assume a page not listed here already matches this guide.

## Accessibility Pass (2026-08) — superseded
An initial pass replaced every white-on-gradient/white-on-accent-fill pairing with a WCAG AA-compliant alternative (dark plum text, or white text over a purpose-built dark "scrim" backdrop), following a review with Accessibility Insights for Web. That version is no longer the site's direction: per explicit developer direction, matching the reference mockups' literal look now takes priority over certified contrast compliance, so white text is back directly on the open gradient in several places (hero heading, nav, footer) without a guaranteed-compliant backdrop behind it. If a specific element is hard to read in practice, treat that as a concrete bug report on that element, not a reason to reintroduce the old backdrop system everywhere.

The `:focus-visible` keyboard focus rings added during that pass are unrelated to the contrast question and are still in place — see Deliberately Deferred below.

## Deliberately Deferred
Disabled states beyond the ones already defined (`.input-pill:disabled`, `.modal-delete-button:disabled`) are a follow-up as new controls need them. The dark rounded frame visible around the whole app in some early reference mockups was confirmed to be the design mockup's artboard framing, not real UI — it is intentionally not implemented.

Hover/active states are defined; keyboard focus indicators are defined via `:focus-visible` (visible on keyboard focus, suppressed on mouse click, matching this style's existing click-vs-hover distinction) on `.input-pill`, `.go-button`, and `.navbar-toggler`. The same `:focus-visible` pattern should be applied to `.enter-button`, `.color-swatch-picker`, `.my-events-go-button`, and the modal buttons the next time their page is touched — they still use the older `box-shadow: none` (no replacement) pattern today.
