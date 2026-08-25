# WhenWorks Style Guide

This document explains the *intent* behind the site's visual style — the "why," not the "what." For the actual color/shape/font values, see the `:root` custom properties block at the top of `wwwroot/css/site.css` — that file is the single source of truth for values, so nothing here should need updating just because a value changes.

## Vibe
Soft, bubbly, bright, happy, airy. Driven by reference mockups (a warm animated gradient background, pill-shaped controls with a "3D pop" bottom-edge shadow, soft white-glass panels, a rounded display font, two-color headings) that this file's token set is deliberately styled to match closely — including matching the mockups' own use of white text directly on the open gradient, which the site does **not** try to certify as WCAG-compliant (see Accessibility Pass below). On top of the static mockup look, the shared layout and Home also carry a set of ambient, cursor-driven effects (ambient spotlight, per-element proximity glow, shiny-text sweep, card sparkles) — see their own section below.

## Color Tokens

### `--gradient-page` / `--gradient-color-1` / `--gradient-color-2` / `--gradient-color-3`
The site's signature background — a warm pink → coral → gold diagonal wash, applied at the `body` level so it's consistent across every page. `--gradient-page` is the static CSS fallback (shown if canvas/JS is ever unavailable, and wherever the drawn blobs don't fully cover). The actual animated effect is layered, independently-drifting radial "blobs" painted on `#ww-gradient-canvas` by `site.js`, built from the same three core hues as `--gradient-color-1/2/3` — a plain-CSS/canvas approximation of a shader-driven reference component, since this project has no WebGL/React runtime for the original. `site.js` keeps its own copies of these three hues rather than reading them via `getComputedStyle`, so **both places must be updated together** if these ever change. Disabled under `prefers-reduced-motion`.

### `--color-accent-yellow-fill` / `--color-accent-yellow-border`
Fill/border pair for the "primary" input pill (e.g. Create Event) and the account dropdown (its toggle and the panel it opens). Keep yellow associated with the primary/creating action as new controls are added.

### `--color-accent-blue-fill` / `--color-accent-blue-border`
Fill/border pair for the "secondary" input pill (e.g. Join Event) and the My Events nav link. Keep blue associated with the secondary/joining action. It's also the fallback fill for navbar controls with no action-color of their own (Login, the hamburger toggler) — blue is the quietest accent that still registers as a control against the pale gradient, and neither Login nor My Events is ever on screen at the same time.

### `--color-accent-green` / `--color-accent-green-border`
Reserved for the "GO!" pattern (`.go-button`, `.enter-button`, `.my-events-go-button`) and the nav's one primary CTA (Register) — a bright pop against the gradient. Use sparingly; it loses its "go"/primary prominence if applied broadly to ordinary buttons.

### `--color-accent-orange-fill`
The hover fill for rows in the My Events list — distinct from the yellow the rows rest at. Not used as a standalone accent elsewhere; if a new element needs an "orange," check whether it's really this hover state before introducing a new token.

### `--color-accent-selected-fill`
The *selected* row fill in My Events — deliberately a different color from the orange hover fill above so hovering and selecting always read as two distinct states, even when a selected row is also hovered.

### `--color-accent-red` / `--color-accent-red-border`
Destructive actions (`.modal-delete-button`, the Logout dropdown item's hover state, the delete-confirmation radio's checked state).

### `--color-accent-pink-fill`
Represents real product data: the default participant color (kept in sync with `Common/ModelConstants.DefaultParticipantColor` via an inline `<style>` override in `_Layout.cshtml`, not hardcoded twice). Drives the "WhenWorks." brand-link hover color and a handful of `::selection` overrides on surfaces where it measures better than the default dark-plum selection color (see `::selection` rules in site.css). Never repurpose this token for a decorative accent — introduce a new one instead, so it stays a true mirror of the real default color.

### `--color-brand-badge-fill`
The small circular sparkle badge next to the wordmark uses this instead of `--color-accent-pink-fill` — kept as its own token specifically so a change to the default *participant* color (data) can never accidentally also change the *brand mark's* color (identity/chrome). Same underlying pink family, deliberately separate variables.

### `--color-text-on-gradient`
White. Used directly on the open gradient for large/bold headings (`.ww-hero-title`, nav, footer) — matching the reference mockups exactly rather than routing everything through a compliant backdrop. See Accessibility Pass below for why.

### `--color-text-strong`
A dark, dusty gray (`#514c54`) — the default dark text color on light surfaces: accent fills (yellow/blue/green pills, GO button interiors), white-glass tags/cards (`.ww-hero-badge`, `.ww-feature-card`), and the `.text-on-gradient` utility. Mirrored as a literal in `Services/ColorContrastHelper.cs` (see Personalized User Color below) — update both together if this value ever changes.

### `--color-accent-brand-glow` (`#dfff5c`)
The bright yellow-green accent from the reference mockups — the hero heading's second line (`.ww-hero-title-accent`) and the brand wordmark's trailing period (`.ww-brand-dot`).

### `--color-chrome-tint` / `--color-chrome-tint-strong`
A shared dark-plum glass tint — one consistent "chrome" treatment applied flat (no `backdrop-filter` blur) to the navbar, the hero badge, the footer's copyright pill, and the feature-card band, so these read as one family of surface instead of a different treatment per element. `-strong` is the same hue, near-opaque, currently used only as the sitewide `::selection` fallback (dark enough that white text still reads over it). New "chrome" surfaces (a bar, a banner, an eyebrow chip sitting directly on the gradient) should reuse this token rather than inventing a new tint.

## Shadow & Shape Tokens

### `--radius-pill`
Everything interactive (nav links, buttons, inputs) is a full pill, not a softly-rounded rectangle — a deliberate signature of the "bubbly" vibe.

### `--shadow-hard` / `--shadow-hard-hover` / `--shadow-hard-active` / `--shadow-hard-focus`
The site's one shadow language: a solid offset block with no blur, in one neutral dark plum (`--shadow-hard-color`) regardless of the element's own accent — the reference mockups' "3D pop" bottom edge. Every interactive control uses one of these four (never a bespoke blurred/tinted shadow), expressing state by height: taller on hover, shorter while pressed, tallest of all (`-focus`) for a container whose child input is actively focused (e.g. `.ww-pill-group:focus-within`). Each state pairs with a matching `translateY` on the same rule.

## Typography Tokens

### `--font-display` (Fredoka)
Reserved for short, large text: headings, button labels. Not legible at small sizes or in long-form text — never use it for body copy, form labels, or validation messages.

### `--font-body` (Nunito)
The default readable typeface for everything else — labels, paragraphs, validation messages, general UI text. Site-wide letter-spacing is tightened slightly (`body { letter-spacing: -0.015em }`) since the font's own default tracking read looser than the reference mockups.

### `--font-mono` (DM Mono)
Small "eyebrow"/code-like text only — currently the Join event-code input. Not a body font.

### Root font size
`html { font-size }` is scaled up (17.5px, 20px from the `md` breakpoint) rather than left at the browser default 16px — the site read too small/sparse at 100% zoom otherwise. Nearly every size elsewhere in the stylesheet is `rem`-based, so this one change scales the whole site; don't reach for a one-off `rem` override to compensate for perceived size instead of adjusting the actual element.

## White-Glass vs. Chrome-Tint vs. Dark Text
Three backdrop-free surface styles recur across the page, and which one an element uses decides its text color and border treatment:
- **Open gradient, no backdrop** (hero heading/subtitle, nav, footer) — white text (`--color-text-on-gradient`), matching the reference mockups directly.
- **Chrome tint** (`--color-chrome-tint`) — the navbar, hero badge, feature band, footer pill. A flat dark-plum glass, always paired with white text and a thin white-ish border; used for the site's structural/decorative "bars and chips," not content cards.
- **White-glass** (`.ww-hero-badge`, `.ww-feature-card`, the account dropdown's own rows, nav pills with no accent of their own) — a translucent white fill (roughly 55–85% opacity) with dark dusty-gray text (`--color-text-strong`). This is the default for any new small tag/card/panel — it needs no special reasoning, it's just dark-on-light. Note it works as a *surface* far better than as a *control*: white-glass buttons sitting directly on the gradient (Login, the hamburger toggler) were hard to pick out and now take an accent fill instead.

None of these use `backdrop-filter` blur — a blurred glass surface on top of the site's flat `--shadow-hard` language read as two conflicting depth treatments, so every "glass" surface here is a flat tint/opacity instead.

Ordinary body content elsewhere (paragraphs, labels, form controls, tables) stays Bootstrap's normal dark text by default.

## Proximity Glow & Ambient Spotlight
A cursor-reactive layer sits on top of the static mockup look, ported by hand (no new package/framework) from the reactbits.dev "Magic Bento" concept:

- **Proximity border** — every "pill container" (nav links/toggle, hamburger toggler, hero badge, brand badge, the Create/Join capsules, the account dropdown panel, the feature cards) exposes `--glow-intensity` (0–1, written by `site.js` from real cursor distance, not `:hover`), and blends its own `border-color` from a resting tone (`--glow-rest-border`) toward its own accent (`--card-glow-rgb`) as the cursor nears. Because it animates the real border rather than painting something extra, it's seamless.
- **Interior tint** (`--glow-tint-shadow`) — an inset shadow using the same accent, so the glow reads as a color even through a pale/pastel pill fill that would otherwise wash a passing spotlight out to white.
- **Page-wide spotlight** (`#ww-spotlight`) — a soft radial glow, positioned by `site.js`, that follows the cursor across the whole shared layout (not just the feature cards). It paints *below* real page content, so every translucent "glass" surface automatically lets it bleed through with no per-element opt-out, and opaque surfaces block it outright. Its color live-updates to whichever element's `--card-glow-rgb` is under the cursor (boosted in saturation), falling back to the gradient's own peach midtone over bare background.
- **Card sparkles** (`.ww-sparkle`) — small flat-colored "confetti" particles spawned by `site.js` on feature-card hover, colored from that card's own `--card-glow-rgb`.

**Rule: targets are outermost containers only.** A control nested inside a pill (`.go-button` inside `.ww-pill-group`, a `.dropdown-item` row inside the account panel) never gets its own glow ring — only the container does. A nested button can still carry its own `--card-glow-rgb` purely so the *page-wide spotlight* tints correctly when the cursor is directly over it (custom properties inherit down the DOM); it just doesn't get a border/tint of its own.

Both the spotlight and the sparkles are purely decorative and fully disabled under `prefers-reduced-motion` (`--glow-intensity` forced to 0, `#ww-spotlight` hidden) — never required to use any control.

## Shiny Text Sweep
`.ww-shiny-text` is a CSS-native gradient-clipped shimmer, layered onto the label/icon *inside* the same set of elements the proximity glow above targets (never applied directly to an element that also owns its own `background-color`, since `background-clip: text` would clip that fill away too — hence the separate inner span). `currentColor`-anchored, so one rule works unmodified on dark pill text or white hero text alike. Deliberately **not** used on long-form body text (`.ww-feature-text`) or on the hero heading itself (its own `text-shadow` doesn't pair cleanly with a transparent fill) — reserve it for short labels/icons, not paragraphs. Also disabled under `prefers-reduced-motion`.

## Personalized User Color
A signed-in user's own chosen color (`ApplicationUser.Color`) drives `--user-color` / `--user-color-text`, set once on the account dropdown's wrapping `<li>` in `_LoginPartial.cshtml` and inherited by "My Profile"/hover states below it. `--user-color-text` is computed **server-side** by `Services/ColorContrastHelper.cs` (WCAG relative-luminance contrast, not a fixed brightness threshold) since CSS alone can't measure contrast against an arbitrary user-picked hex color — it picks whichever of white or `--color-text-strong` reads better. If `--color-text-strong`'s value ever changes, update the literal mirrored in that helper too.

## Current Scope
These tokens are applied to the shared layout (navbar/footer), Home, Events/SignIn, MyEvents/Index (including its delete-confirmation modal), and the Identity account + Manage pages. The proximity-glow/spotlight/sparkle/shiny-text effects above are currently scoped to the shared layout and Home only — they have not been extended to SignIn/MyEvents/Identity, which still use the earlier plain-pill-and-shadow treatment without the ambient layer. Events/Home (the post-join event landing page) hasn't been styled at all yet. Don't assume a page not listed here already matches this guide.

## Accessibility Pass (2026-08) — superseded
An initial pass replaced every white-on-gradient/white-on-accent-fill pairing with a WCAG AA-compliant alternative, following a review with Accessibility Insights for Web. That's no longer the site's direction: per explicit developer direction, matching the reference mockups' literal look now takes priority over certified contrast compliance, so white text is back directly on the open gradient in several places (hero heading, nav, footer) with no guaranteed-compliant backdrop. If a specific element is hard to read in practice, treat that as a bug report on that element, not a reason to reintroduce the old backdrop system everywhere.

The `:focus-visible` keyboard focus rings added during that pass are unrelated to the contrast question and are still in place — see Deliberately Deferred below.

## Deliberately Deferred
Disabled states beyond the ones already defined (`.input-pill:disabled`, `.modal-delete-button:disabled`) are a follow-up as new controls need them. The dark rounded frame visible around the whole app in some early reference mockups was confirmed to be the design mockup's artboard framing, not real UI — intentionally not implemented.

Hover/active states are defined; keyboard focus indicators are defined via `:focus-visible` on `.input-pill`, `.go-button`, and `.navbar-toggler`. The same pattern should be applied to `.enter-button`, `.color-swatch-picker`, `.my-events-go-button`, and the modal buttons the next time their page is touched — they still use the older `box-shadow: none` (no replacement) pattern today.
