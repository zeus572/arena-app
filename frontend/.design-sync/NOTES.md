# design-sync notes — Debate Arena DS (frontend/)

The synced "design system" is the main Arena app's reusable component set
(`frontend/src/components/` + `src/components/ui/`), styled by Tailwind v4 tokens.
`frontend` is a Vite **app**, not a published library, so the converter runs in
scoped-entry mode (barrel + provider, like the Civic DS). Project: `Debate Arena
Design System` (`e991ce4c-1a22-4579-858f-2af9e8f94b1c`). 19 components.

## ⚠ This is the SECOND design system in this repo

- **Civic Arena DS** lives at the repo-root `.design-sync/` (config `pkg: frontend-civic`)
  and syncs to a DIFFERENT project (`a107f0bd-…`). Do NOT touch repo-root `.design-sync/`
  when working the Debate Arena DS, and vice-versa.
- The converter scripts resolve `previews/`, `.cache/`, `learnings/`, `overrides/` from
  **`resolve('.design-sync')` relative to CWD** (not relative to `--config`). So the two
  DSes are isolated by **CWD**: run every Debate Arena command with **`cwd = frontend/`**,
  which makes its sync home `frontend/.design-sync/`. (There is even a `Button` name
  collision between the two — another reason they must never share a `.design-sync/`.)

## Build / re-sync recipe (run from `frontend/`)

```sh
cd frontend
node ../.ds-sync/package-build.mjs --config .design-sync/config.json \
  --node-modules node_modules --entry .ds-barrel.ts --out ./ds-bundle
node ../.ds-sync/package-validate.mjs ./ds-bundle
# one-command re-sync (first sync omitted --remote; re-syncs add it):
node ../.ds-sync/resync.mjs --config .design-sync/config.json \
  --node-modules node_modules --entry .ds-barrel.ts --out ./ds-bundle \
  --remote .design-sync/.cache/remote-sync.json
```

- **`--entry .ds-barrel.ts` is required.** `frontend/.ds-barrel.ts` (committed,
  dot-prefixed → outside tsconfig.app's `include:["src"]`) re-exports exactly the 19 DS
  components + the preview provider, scoping the esbuild bundle. Without it the converter
  tries to bundle `main.tsx` → `index.css` → `@import 'tailwindcss'` (esbuild can't process).
- **Provider** = `DesignProvider` (`frontend/.ds-provider.tsx`, committed, re-exported by
  the barrel; `cfg.provider.component`). It is `MemoryRouter > AuthProvider`. Navbar /
  ForkDebateDialog / BreakingTicker use react-router; Navbar reads AuthContext. With no
  stored token, AuthProvider resolves synchronously to the logged-out state (no network).
- **`cfg.tsconfig` = `.tsconfig.sync.json`** (committed, frontend root) — supplies the
  `@/* → ./src/*` alias to esbuild. No `extends`, no `"//"` keys (the converter's
  tsconfig-paths plugin doesn't follow extends and its comment-stripper corrupts `"//"` keys).
- **`cfg.cssEntry` = the compiled Tailwind CSS** `dist/assets/index-<hash>.css` (the full
  app build, NOT `src/index.css` which is just `@import 'tailwindcss'`). This is also why
  `_ds_bundle.css` is the whole app's compiled utility set, not a scoped subset.

## componentSrcMap (19) + what's excluded

All 19 are explicitly pinned in `cfg.componentSrcMap`. Note **`format-layouts.tsx` exports
8 components** (LoveMeter, HotSeatHeader, LaughterMeter, TweetHeader, RapidFireBanner,
LongformHeader, CommonGroundHeader, RoastStageHeader) — all mapped to that one file.
`DesignProvider` is mapped to `null` (stays in the bundle as the provider, excluded as a
card). `ProtectedRoute` is intentionally NOT synced (routing logic, not UI). Helper exports
(`computeFireLevel`, `getFormatBubbleStyles`, `buttonVariants`, `BACKDROP_ACCENTS`) are
camelCase/UPPER and not picked up as components.

## Preview-authoring learnings (all 19 authored, graded good)

- **Overlay components** (`fixed inset-0`) don't render in-frame: the fixed root resolves
  against the viewport, so the backdrop doesn't fill the card and content clips. Fix in the
  preview by wrapping in a **containing block** — a div with `transform: translateZ(0)` +
  bounded width/height makes `position:fixed` resolve against the wrapper. Used for
  `ForkDebateDialog` (also `cfg.overrides {cardMode:"single", viewport:"640x520"}`) and for
  each `DebateBackdrop` cell.
- **`DebateBackdrop`** is `fixed inset-0 -z-10`; even wrapped it trips `[GRID_OVERFLOW]`
  (the checker flags any `fixed` descendant). Set `cfg.overrides {cardMode:"single",
  primaryStory:"Arcade", viewport:"560x260"}`. All 4 themes still render in the review sheet
  and are addressable via `?story=`.
- **`MatchupIntro`** has a `preview` prop that renders inline (relative 16rem panel), but its
  entrance animations still PLAY (the `, none` it appends doesn't disable the first
  animation), so the capture caught them mid-flight (avatars landed, VS/names/topic not yet
  faded in). The preview injects `<style>{`*{animation:none!important}`}</style>` to freeze
  every layer at its visible resting state.
- **Dark-designed format headers** (`HotSeatHeader`, `LaughterMeter`, `RapidFireBanner`) use
  light text on low-opacity dark gradients meant for the dark debate page — washed out on a
  white card. Their previews wrap the component in a dark surface
  (`linear-gradient(160deg,#1c1530,#0c0a09)`). `RoastStageHeader` ships an opaque dark bg, so
  it needs no wrapper. `LoveMeter`/`CommonGroundHeader`/`TweetHeader`/`LongformHeader` are
  light-designed and render fine on the card.
- Wide banners (`Navbar`, `BreakingTicker`, `MatchupIntro`, all format headers) use
  `cfg.overrides {cardMode:"column"}` so each gets a full-width row.
- `RollingNumber` animates 0→value on in-view; the capture catches it a frame before settling
  (e.g. 1276 vs 1284). Graded good — reads as a live stat. Not a regression.
- Previews import from the package name **`"frontend"`**; layout glue uses inline styles or
  token-backed utilities (the agent-color families + shadcn semantic tokens).

## Known render warns (triaged — not new on re-sync)

- `[FONT_MISSING]` Inter / Geist Mono / Cambria — the app ships NO font files and loads none
  at runtime; `--font-sans`/`--font-mono` are CSS font-stack names with system fallbacks
  (`system-ui`/`monospace`). Previews render in fallback fonts, same as the real app.
  Suppressed via `cfg.runtimeFontPrefixes`. (If brand fidelity ever matters, add real woff2 +
  `@font-face` via `cfg.extraFonts` — needs the user's OK.)

## Re-sync risks (watch-list)

- **Compiled-CSS hash drift:** `cfg.cssEntry` points at `dist/assets/index-<hash>.css`. The
  hash changes whenever `frontend` is rebuilt (`npm run build`). Re-point `cfg.cssEntry` at
  the current `dist/assets/index-*.css` on re-sync (rebuild dist first if stale).
- **Component set changes:** if `frontend` adds/removes/renames a synced component, update BOTH
  `frontend/.ds-barrel.ts` AND `cfg.componentSrcMap`. Especially watch `format-layouts.tsx`
  (8 components in one file).
- `Button` is exported as `export { Button }` (named). Any future default-exported component
  needs `export { default as X }` in the barrel.
- The barrel / provider / `.tsconfig.sync.json` live INSIDE `frontend/` (dot-prefixed,
  committed, outside the app's tsconfig include globs, so the app build ignores them).
- gitignore: repo-root `.gitignore` anchors `.design-sync/.cache/` etc. to the root, so
  explicit `frontend/.design-sync/.cache|learnings|node_modules` entries were added.
  `ds-bundle/` (matches any depth) already covers `frontend/ds-bundle/`.
- The preview overlay/dark-wrapper/animation-freeze tricks are tied to the components' current
  markup (fixed positioning, dark gradients, the `preview` prop). If those components are
  reworked upstream, re-check the affected previews.
