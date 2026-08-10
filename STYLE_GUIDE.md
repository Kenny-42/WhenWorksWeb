# WhenWorks Style Guide

This document explains the *intent* behind the site's visual style — the "why," not the "what." For the actual color/shape/font values, see the `:root` custom properties block at the top of `wwwroot/css/site.css` — that file is the single source of truth for values, so nothing here should need updating just because a value changes.

## Vibe
Soft, bubbly, bright, happy, airy. Driven by reference mockups attached to epic #40 ("Style the website with CSS") showing the Home page's Create/Join Event flow on a warm gradient background with pill-shaped controls and a rounded display font.

## Tokens

### `--gradient-page`
The site's signature background — a warm two-color diagonal gradient. Applied at the `body` level so it's consistent across every page, not just certain screens.

### `--color-accent-yellow-fill` / `--color-accent-yellow-border`
Fill/border pair for the "primary" input pill (e.g. Create Event). Keep yellow associated with the primary/creating action as new controls are added.

### `--color-accent-blue-fill` / `--color-accent-blue-border`
Fill/border pair for the "secondary" input pill (e.g. Join Event). Keep blue associated with the secondary/joining action.

### `--color-accent-green`
Reserved for small, circular, high-emphasis action buttons (the "GO!" pattern) — a bright pop against the gradient. Use sparingly; it loses its "go" prominence if applied broadly to ordinary buttons.

### `--color-text-on-gradient`
Text/border color for anything sitting directly on `--gradient-page` (navbar, footer, headings on the gradient). Always white-on-gradient — dark text was tried against the mockups' pink/yellow gradient and reads muddy, so don't introduce dark text on top of the gradient.

### `--radius-pill`
Everything interactive (nav links, buttons, inputs) is a full pill, not a softly-rounded rectangle. This is a deliberate signature of the "bubbly" vibe, not an ordinary Bootstrap `border-radius` — apply it fully to new interactive controls in this style rather than a partial radius.

### `--font-display` (Cherry Bomb One)
Reserved for short, large text: headings and button labels. It is not legible at small sizes or in long-form text — never use it for body copy, form labels, or validation messages.

### `--font-body` (Quicksand)
The default readable typeface for everything else — labels, paragraphs, validation messages, general UI text.

## Body Text vs. the Gradient
The gradient background (`--gradient-page`) sits directly behind `<body>` on every page, but most page content is *not* meant to read as white-on-gradient — only elements with nothing else underneath them (nav, footer, a bare page heading) should. The default for ordinary content (paragraphs, labels, form controls, tables) stays Bootstrap's normal dark text, which reads fine against both ends of the gradient and against any white/card surfaces a page adds later (e.g. error pages).

Use the **`.text-on-gradient`** utility class (`wwwroot/css/site.css`) to opt a specific element into white text when it sits directly on the raw gradient with no card or panel behind it. Don't apply it broadly or make it a default — it's meant to be added per-element as pages are styled, not inherited automatically.

## Current Scope
As of this writing, these tokens are applied to the shared layout (navbar/footer) and the Home page only — see `Spec/Features/FEATURES-shared-layout-style-guide.ospec`. Other pages still use default Bootstrap styling and will pick up this style guide incrementally as their own styling issues under epic #40 are completed. Don't assume every page already matches this guide.

## Deliberately Deferred
Hover/focus/active/disabled states are not yet defined for the new pill controls — that's a follow-up pass once the base look is confirmed to be right. Don't invent one-off hover styles ahead of that decision. The dark rounded frame visible around the whole app in the reference mockups was confirmed to be the design mockup's artboard framing, not real UI — it is intentionally not implemented.
