# Civic Arena — building with these components

A magazine-style civic UI. Components are plain React, styled with **Tailwind
utility classes** plus a set of **CSS custom-property tokens**. Import everything
from the bundle global `window.CivicArena` (loaded from `_ds_bundle.js`).

## Required setup — wrap the tree

Two wrappers are mandatory or components render wrong:

1. **`theme-magazine`** — every token (`--accent`, fonts, surfaces) is defined on a
   `.theme-magazine` class, NOT on `:root`. Without it, accent fills render
   invisible (white-on-unset) and the serif display font falls back.
2. **A Router** — many components use react-router `<Link>`/`useNavigate` and throw
   or render blank outside a Router.

`DesignProvider` (exported from the bundle) supplies both. Wrap once at the root:

```jsx
const { DesignProvider, Button, ValueChip } = window.CivicArena;

<DesignProvider>            {/* = <MemoryRouter><div className="theme-magazine"> */}
  <div className="mx-auto max-w-2xl p-8">
    <h1 className="display text-3xl text-[var(--fg)]">Pick your priorities</h1>
    <div className="mt-4 flex flex-wrap gap-2">
      <ValueChip label="Lower taxes" selected />
      <ValueChip label="Public schools" />
    </div>
    <Button variant="primary" className="mt-6">Co-sign bill</Button>
  </div>
</DesignProvider>
```

If you can't use `DesignProvider`, replicate it: a router around a
`<div className="theme-magazine">`.

## Styling idiom — Tailwind utilities + token vars

Style with Tailwind utility classes. For brand color/typography, reference the
tokens through Tailwind arbitrary values — `bg-[var(--accent)]`, `text-[var(--fg)]`,
`border-[var(--border)]`. The full token set (defined on `.theme-magazine`):

| Token | Role |
|---|---|
| `--bg` / `--bg-elev` | page background / elevated surface (cards) |
| `--fg` / `--fg-soft` / `--muted` | text: primary / secondary / tertiary |
| `--border` | hairline borders |
| `--accent` | brand action color (warm red-orange); primary buttons, selected states |
| `--federal` / `--federal-soft` / `--state` / `--state-soft` | civic data accents (gov vs state) |
| `--font-display` | serif display (headings, pull quotes) — used via the `display` class |
| `--font-body` | body sans |
| `--text-base` / `--radius` | base font size / corner radius |

The `display` class applies the serif display font. Don't invent token names —
these are the only ones defined.

## Where the truth lives

- Stylesheet: the bound `styles.css` and its `@import`ed `_ds_bundle.css` (component
  styles) and token CSS — read these before styling.
- Per component: `<Name>.d.ts` (the prop contract) and `<Name>.prompt.md` (usage),
  in each component's folder.

## Notes

- Some components fetch data on mount (e.g. `CountdownTimer`, `BudgetFactCard`) and
  expect a live API — they show a loading state in a static design.
- `Button` also exports `ButtonLink` (a router `<Link>` styled as a button) and
  `buttonClasses` (the class-string builder) for non-button elements.

# CivicArena (frontend-civic@0.0.0)

This design system is the published frontend-civic React library, bundled as a single
browser global. All 24 components are the real upstream code.

## Where things are

- `_ds_bundle.js` — the whole-DS bundle at the project root; loads every component to `window.CivicArena`. First line is a `/* @ds-bundle: … */` metadata header.
- `styles.css` — the single stylesheet entry: it `@import`s the tokens, fonts, and component styles (`_ds_bundle.css`). Link this one file.
- `components/<group>/<Name>/<Name>.prompt.md` (example JSX + variants), `<Name>.d.ts` (types), `<Name>.html` (variant grid).
- `tokens/*.css` — CSS custom properties, names verbatim from upstream.
- `fonts/` — `@font-face` files + `fonts.css` (when the package ships fonts).

For a specific component, `read_file("components/<group>/<Name>/<Name>.prompt.md")`.

## Loading

Add these two lines to your page once (React must be on the page first):

```html
<link rel="stylesheet" href="styles.css">
<script src="_ds_bundle.js"></script>
```

Components are then available at `window.CivicArena.*`. Mount into a dedicated child node (e.g. `<div id="ds-root">`), not the host page's own React root, so the two trees don't collide:

```jsx
const { BottomTabs } = window.CivicArena;
ReactDOM.createRoot(document.getElementById('ds-root')).render(<BottomTabs />);
```

Wrap the tree in the provider — most components read theme/i18n from context:

```jsx
<DesignProvider>{children}</DesignProvider>
```

## Tokens

166 CSS custom properties from frontend-civic. Names are
preserved verbatim from upstream. They are declared inside `_ds_bundle.css` (this DS ships one compiled stylesheet rather than separate token files).

- **color** (74): `--tw-border-style`, `--tw-shadow-color`, `--tw-inset-shadow-color`, …
- **spacing** (5): `--tw-space-y-reverse`, `--tw-inset-shadow`, `--tw-inset-shadow-alpha`, …
- **typography** (17): `--tw-font-weight`, `--tw-tracking`, `--font-sans`, …
- **radius** (6): `--radius-sm`, `--radius-md`, `--radius-lg`, …
- **shadow** (7): `--tw-shadow`, `--tw-shadow-alpha`, `--tw-ring-shadow`, …
- **other** (57): `--tw-translate-x`, `--tw-translate-y`, `--tw-translate-z`, …

## Components

### magazine
- `BottomTabs`
- `BudgetFactCard`
- `Button`
- `CampaignPostCard`
- `CandidateAvatar`
- `CountdownTimer`
- `CoverStory`
- `DisclaimerBadge`
- `Flyout`
- `MobileMenu`
- `NavDropdown`
- `PlayReadToggle`
- `PullQuote`
- `SharePreviewCard`
- `SignInPrompt`
- `Term`
- `ValueChip`

### taxapportionment
- `CaveatGrid`
- `HouseholdCalculator`
- `PonderSection`
- `ScalingTable`
- `SplitBar`
- `StateCard`
- `StatePicker`
