# design-sync notes — Civic Arena DS (frontend-civic)

The synced "design system" is the magazine prototype's component set
(`frontend-civic/src/prototypes/magazine/components/`, 24 components incl. the
`taxApportionment/` subgroup), styled by Tailwind v4 tokens. `frontend-civic` is a
Vite **app**, not a published component library, so the converter runs in a
scoped-entry mode (see below). Project: `Civic Arena Design System`
(`a107f0bd-aa0e-45f8-aafe-23a4b3661074`).

## Build recipe (re-sync)

```sh
node .ds-sync/package-build.mjs --config .design-sync/config.json \
  --node-modules frontend-civic/node_modules \
  --entry frontend-civic/.ds-barrel.ts --out ./ds-bundle
node .ds-sync/package-validate.mjs ./ds-bundle
```

- **`--entry frontend-civic/.ds-barrel.ts` is required.** Without it the converter
  synth-bundles ALL ~66 src files (pulls in `main.tsx` → `index.css` →
  `@import 'tailwindcss'` which esbuild can't process, plus app pages). The barrel
  (`frontend-civic/.ds-barrel.ts`, committed) re-exports exactly the 24 components
  + the preview provider, scoping the bundle. `--entry` also makes PKG_DIR walk up
  to the real `frontend-civic/` dir.
- **Do NOT create a `node_modules/frontend-civic` self-junction.** It "works" for
  resolution but esbuild reading component sources *through* the junction throws
  Windows `Incorrect function` on the `@/taxModel/engine` directory import. The
  `--entry` walk-up gives a real PKG_DIR instead — no junction needed.
- **`cssEntry` = the compiled Tailwind CSS** (`dist/assets/index-<hash>.css`), NOT
  `src/index.css` (which is just `@import 'tailwindcss'` directives). ⚠ The hash
  changes whenever `frontend-civic` is rebuilt — re-point `cfg.cssEntry` at the
  current `dist/assets/index-*.css` on re-sync (run `cd frontend-civic && npm run build` first if dist is stale).
- **`cfg.tsconfig` = `.tsconfig.sync.json`** (committed, at frontend-civic root).
  It mirrors the app's `@/*` alias and maps the one directory import
  `@/taxModel/engine` → its `index.ts` via a **wildcard** key
  (`@/taxModel/engine*`). Two gotchas in the converter's `tsconfigPathsPlugin`:
  (1) it does NOT follow `extends`; (2) its comment-stripper corrupts a `"//"`
  JSON *key* (turns the file invalid → paths silently disabled). So: no `extends`,
  no `"//"` keys in that file, and engine must be a `*`-key (a non-wildcard literal
  paths key makes esbuild drop the whole map).
- **Provider**: `DesignProvider` (MemoryRouter) in `frontend-civic/.ds-provider.tsx`
  (committed, re-exported by the barrel; `cfg.provider.component = "DesignProvider"`).
  9 of the 24 components use react-router `<Link>`/`useNavigate` and render blank
  without it.

## Known render warns (triaged — not new on re-sync)

- `[FONT_MISSING]` Inter / Iowan Old Style / Charter / Source Serif Pro — the
  repo ships NO font files (`brand-assets/` is PNGs only); these are the
  serif-display + sans stacks the app expects the environment to provide.
  Suppressed via `cfg.runtimeFontPrefixes`. Previews render in system fallback
  fonts. If brand fidelity matters later, add real woff2 + `@font-face` via
  `cfg.extraFonts` (needs the user's OK).
- `[RENDER_BLANK]` BottomTabs / Button / MobileMenu — floor cards that render
  empty without props/children (Button with no children = invisible pill). These
  resolve once their `.design-sync/previews/*.tsx` are authored.

## Re-sync risks

- **Compiled-CSS hash drift** (above): `cfg.cssEntry` points at a hashed dist file.
- The barrel, `.ds-provider.tsx`, and `.tsconfig.sync.json` live INSIDE
  `frontend-civic/` (dot-prefixed, committed, outside the app's tsconfig include
  globs so the app build ignores them). If `frontend-civic` adds/removes/renames a
  magazine component, update `.ds-barrel.ts` AND `cfg.componentSrcMap`.
- `Flyout` is a **default** export — the barrel uses `export { default as Flyout }`.
  Any other default-exported component needs the same.

## Preview authoring notes (all 24 authored)

- **Card-mode overrides** (`cfg.overrides`, already set): `Flyout` (full-screen
  slide-over), `MobileMenu` (md:hidden drawer that portals to body), and `BottomTabs`
  (md:hidden fixed bottom bar) only render inside a single narrow mobile frame —
  hence `{cardMode:"single", viewport:"390x780"|"480x720"}`. Without it they paint
  empty at the default wide multi-cell viewport.
- **CountdownTimer** fetches `getNextElection` on mount; with no backend it renders
  only its styled "Loading…" state. Its preview is graded on that honest static
  state — the live day/hr/min/sec clock cannot render statically. (Known, not a regression.)
- **Tax engine is not on the bundle global** — there's no `src/index.ts` barrel
  re-exporting `compute*`/`STATE_PROFILES`, so taxApportionment previews construct
  `StateProfile`/`TaxComputeResult` props inline. `SplitBar` shares are fractions
  (0..1); `ScalingTable` only needs `filing`+`profile`+`currentIncome` (runs the
  engine itself); `HouseholdCalculator` is fully controlled (handlers can be no-ops).
- `CandidateAvatar` PNGs don't load here (no `/avatars/*`); it correctly falls back
  to the colored-initials disc — that's the intended preview render.

## Known render warns (triaged — not new on re-sync)

- `[FONT_MISSING]` (Inter/Iowan Old Style/Charter/Source Serif Pro) — see above,
  suppressed via `runtimeFontPrefixes`.
- `[RENDER_THIN]` on **Flyout** and `[RENDER_BLANK]` on **MobileMenu** persist even
  WITH the card-mode overrides — benign: `fixed inset-0` / body-portal positioning
  makes the measured root height read 0, but the screenshots confirm the panel/drawer
  render fully and styled. Graded good. (BottomTabs render-check is clean post-override.)
- `[RENDER_ERRORS] CountdownTimer: AxiosError Network Error` — expected; it fetches
  on mount and there's no backend, so it shows the styled loading state.
- `HouseholdCalculator` uses `cfg.overrides {cardMode:"column"}` so the wide
  calculator gets a full-width row instead of cropping in the grid.
