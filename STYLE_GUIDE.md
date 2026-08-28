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
The site's global "hot pink" accent — this variable (defined once in site.css's `:root`) is the single source of truth for that hue, freely reused for decorative/interactive treatments: the "WhenWorks." brand-link hover color, `.text-link`'s hover/focus color, the checked fill on every checkbox site-wide, and a handful of `::selection` overrides on surfaces where it measures better than the default dark-plum selection color (see `::selection` rules in site.css). `Common/ModelConstants.DefaultParticipantColor` (the default participant/user color) is a literal C# copy of this same value, not the other way around — CSS can't be read from C# at runtime, so the sync is manual, but conceptually this variable leads and that constant follows. Update both together if this value ever changes.

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

- **Proximity border** — an element can expose `--glow-intensity` (0–1, written by `site.js` from real cursor distance, not `:hover`), and blend its own `border-color` from a resting tone (`--glow-rest-border`) toward its own accent (`--card-glow-rgb`) as the cursor nears. Because it animates the real border rather than painting something extra, it's seamless. **Live-blending is reserved for elements that also get the card-sparkle confetti below — currently just `.ww-feature-card` — plus one deliberate exception, `.ww-brand-badge`** (the navbar's sparkle-icon wordmark badge; no confetti of its own, kept glowing anyway since it's already the site's literal "sparkle" element). `site.js`'s `glowTargets` list is what actually drives the live blend — only those two selectors are in it.

  Most other "pill container" elements (nav links/toggle, the hamburger toggler, the hero badge, the Create/Join capsules, the account dropdown panel, the Event Sign-In card, the Identity account cards, the Manage sidebar's nav links) still write `border-color: var(--glow-border-color)` and share the same shared CSS block that defines `--glow-intensity`/`--glow-rest-border`/`--glow-border-color`/`--glow-tint-shadow` — but since they're never in `glowTargets`, their `--glow-intensity` never moves off that block's own default of `0`, which makes `--glow-border-color` permanently resolve to `--glow-rest-border` (a plain static accent, no live blend) and `--glow-tint-shadow` permanently transparent. This keeps their CSS expressions valid without a second, simpler code path — don't "clean this up" by hardcoding a literal border color on one of these elements; that would just be a different way of writing the same resting color while losing the shared machinery other elements still need. If a new element should join the *live* set, it needs sparkle confetti (or an equally deliberate exception, argued the way `.ww-brand-badge` was) — being a glow-capable pill container alone is no longer sufficient reason.
- **Interior tint** (`--glow-tint-shadow`) — an inset shadow using the same accent, so the glow reads as a color even through a pale/pastel pill fill that would otherwise wash a passing spotlight out to white. Same live/static split as the border above.
- **Page-wide spotlight** (`#ww-spotlight`) — a soft radial glow, positioned by `site.js`, that follows the cursor across the whole shared layout, independent of the border-glow scope above. It paints *below* real page content, so every translucent "glass" surface automatically lets it bleed through with no per-element opt-out, and opaque surfaces block it outright. Its color live-updates to whichever element's `--card-glow-rgb` is directly under the cursor (boosted in saturation, read straight off `event.target` — not `glowTargets`), falling back to the gradient's own peach midtone over bare background. This is why elements outside the live-glow set (nav links, `.go-button`, etc.) still carry their own `--card-glow-rgb` — it does nothing for their own border, but still tints the spotlight correctly while the cursor passes over them.
- **Card sparkles** (`.ww-sparkle`) — small flat-colored "confetti" particles spawned by `site.js` on feature-card hover, colored from that card's own `--card-glow-rgb`. Home-only (see Deliberately Deferred below for why Sign-In/account cards don't get this too).

**Rule: targets are outermost containers only.** Within the live-glow set, a control nested inside a pill (`.go-button` inside `.ww-pill-group`, a `.dropdown-item` row inside the account panel) never gets its own glow ring — only the container does. A nested button can still carry its own `--card-glow-rgb` purely so the *page-wide spotlight* tints correctly when the cursor is directly over it (custom properties inherit down the DOM); it just doesn't get a border/tint of its own.

Both the spotlight and the sparkles are purely decorative and fully disabled under `prefers-reduced-motion` (`--glow-intensity` forced to 0, `#ww-spotlight` hidden) — never required to use any control.

## Shiny Text Sweep
`.ww-shiny-text` is a CSS-native gradient-clipped shimmer, layered onto the label/icon *inside* the same set of elements the proximity glow above targets (never applied directly to an element that also owns its own `background-color`, since `background-clip: text` would clip that fill away too — hence the separate inner span). `currentColor`-anchored, so one rule works unmodified on dark pill text or white hero text alike. Deliberately **not** used on long-form body text (`.ww-feature-text`) or on the hero heading itself (its own `text-shadow` doesn't pair cleanly with a transparent fill) — reserve it for short labels/icons, not paragraphs. Also disabled under `prefers-reduced-motion`.

## Personalized User Color
A signed-in user's own chosen color (`ApplicationUser.Color`) drives `--user-color` / `--user-color-text`, set once on the account dropdown's wrapping `<li>` in `_LoginPartial.cshtml` and inherited by "My Profile"/hover states below it. `--user-color-text` is computed **server-side** by `Services/ColorContrastHelper.cs` (WCAG relative-luminance contrast, not a fixed brightness threshold) since CSS alone can't measure contrast against an arbitrary user-picked hex color — it picks whichever of white or `--color-text-strong` reads better. If `--color-text-strong`'s value ever changes, update the literal mirrored in that helper too.

## Current Scope
These tokens are applied to the shared layout (navbar/footer), Home, Events/SignIn, MyEvents/Index (including its delete-confirmation modal), and the Identity account + Manage pages. The spotlight/shiny-text effects are now also on Events/SignIn (its `.ww-hero-badge` inherited the shiny-text/spotlight-tint effect automatically by reusing that class) and on the full Identity account + Manage section (`.ww-account-card` reuses the same cream-card/hard-shadow language as `.ww-signin-card`; the Manage sidebar's `.manage-nav-link` pills get the same shiny-text/hover-lift treatment as the navbar's own nav links) — but the *live* proximity-glow border and card-sparkle particles stay reserved for `.ww-feature-card` (plus `.ww-brand-badge` as glow's one exception), per this file's own Proximity Glow section above. MyEvents still uses the earlier plain-pill-and-shadow treatment without any of the ambient layer. Events/Home (the post-join event landing page) hasn't been styled at all yet. Don't assume a page not listed here already matches this guide.

### `.ww-account-card`
The Identity account pages' (Login, Register, ForgotPassword, ResetPassword, ResendEmailConfirmation, both confirmation pages, and the whole Manage section) equivalent of `.ww-signin-card` — an opaque cream card holding each page's form, instead of the bare text-on-gradient layout those pages used before. Two accent variants, `.ww-account-card-yellow`/`-blue`, mirror `.ww-pill-group-yellow`/`-blue`'s meaning (yellow primary/local-account action, blue secondary/alongside action — e.g. Login/Register's external-providers column). `.ww-account-card-delayed` staggers a second card's fade-up entrance, same as `.ww-signin-card`'s own delay against its intro column. Manage's version is wrapped once in `Areas/Identity/Pages/Account/Manage/_Layout.cshtml` rather than per-page, since every Manage page already renders the same heading + divider pattern into it.

Text inside stays dark with no color-class changes needed: `.text-on-gradient` (the utility class, not the `--color-text-on-gradient` token) already resolves to `--color-text-strong`, so it reads correctly on both the open gradient and this card's cream fill. Two things that *did* need dark-specific overrides once wrapped in a card: `.account-divider` (the bold white version used directly on the gradient nearly disappears on cream, so `.ww-account-card .account-divider` restates it dark at low opacity) and `.external-login-button`/`.modal-cancel-button` (normally a white-outlined pill meant for the open gradient — `.ww-account-card` scopes both to a dark outline instead).

## Accessibility Pass (2026-08) — superseded
An initial pass replaced every white-on-gradient/white-on-accent-fill pairing with a WCAG AA-compliant alternative, following a review with Accessibility Insights for Web. That's no longer the site's direction: per explicit developer direction, matching the reference mockups' literal look now takes priority over certified contrast compliance, so white text is back directly on the open gradient in several places (hero heading, nav, footer) with no guaranteed-compliant backdrop. If a specific element is hard to read in practice, treat that as a bug report on that element, not a reason to reintroduce the old backdrop system everywhere.

The `:focus-visible` keyboard focus rings added during that pass are unrelated to the contrast question and are still in place — see Deliberately Deferred below.

## Deliberately Deferred
Disabled states beyond the ones already defined (`.input-pill:disabled`, `.modal-delete-button:disabled`) are a follow-up as new controls need them. The dark rounded frame visible around the whole app in some early reference mockups was confirmed to be the design mockup's artboard framing, not real UI — intentionally not implemented.

Hover/active states are defined; keyboard focus indicators are defined via `:focus-visible` on `.input-pill`, `.go-button`, `.navbar-toggler`, `.enter-button`, `.color-swatch-picker` (the latter two were updated when Events/SignIn was next touched, per this note's own instruction), and now also `.modal-cancel-button`, `.modal-delete-button`, `.external-login-button`, and `.manage-nav-link` (updated when the Identity account + Manage pages were next touched). `.my-events-go-button` still uses the older `box-shadow: none` (no replacement) pattern — apply the same `--shadow-hard`-family treatment there the next time MyEvents/Index is touched.

Events/SignIn's own card, and the Identity account pages' `.ww-account-card`, intentionally do **not** get Home's card-sparkle confetti particles or its big hover-bounce, even though both are proximity-glow targets like `.ww-feature-card` — those two effects are tuned for a small hoverable tile the cursor passes over, and would fight a large static form the visitor is actively typing into. If a future page adds another large "glow-target" card like this, default to skipping sparkles/bounce there too unless there's a specific reason to add them.
