# Debate Arena — building with this design system

A React + Tailwind v4 component set for an AI political-debate platform. Components are
real compiled exports on `window.DebateArena.*`; their styles ship in `styles.css`
(which `@import`s the app's full compiled Tailwind stylesheet, `_ds_bundle.css`, with
every design token).

## Wrapping & setup

Wrap the tree in the exported **`DesignProvider`** — it supplies the router and auth
context several components read. Without it, `Navbar`, `ForkDebateDialog`, and
`BreakingTicker` (react-router `Link`/`useNavigate`) and `Navbar` again (auth context)
render blank.

```jsx
const { DesignProvider, Navbar, Button } = window.DebateArena;
<DesignProvider>
  <Navbar />
  {/* your screen */}
</DesignProvider>
```

In a real app the equivalent chain is react-router's `<BrowserRouter>` + the app's
`AuthProvider`. Dark mode is opt-in: add `class="dark"` to a root ancestor (the tokens
below all have dark values).

## Styling idiom — Tailwind v4 utilities + design tokens

Style with Tailwind utility classes. Two token families carry the brand; **prefer these
over raw colors**:

**Semantic (shadcn-style) tokens** — available as utilities *and* `var(--token)`:

| Surface / text | Utility |
|---|---|
| page | `bg-background` / `text-foreground` |
| card | `bg-card` / `text-card-foreground` |
| primary action | `bg-primary` / `text-primary-foreground` |
| secondary | `bg-secondary` / `text-secondary-foreground` |
| muted | `bg-muted` / `text-muted-foreground` |
| destructive | `bg-destructive` / `text-destructive` |
| hairline | `border-border` · radius `rounded-md` (`--radius`) |

**Agent-color lanes** — utility classes only (from `@theme inline`; there is no
`var(--color-*)`). For each of the 7 token-backed lanes — `libertarian`, `progressive`,
`green`, `conservative`, `citizen`, `wildcard`, `commentator`:

- `bg-<lane>` — solid avatar disc (white text)
- `bg-<lane>-bg` — soft chat-bubble background
- `bg-<lane>-tag` + `text-<lane>` — ideology badge

(`celebrity` → `bg-amber-*`, `historical` → `bg-stone-*`.) The `AgentColor` union is:
`"libertarian" | "progressive" | "green" | "conservative" | "citizen" | "wildcard" |
"commentator" | "celebrity" | "historical"`.

`styles.css` is a **compiled** build, not JIT: the broad utility set the app already uses
is present, but an unused arbitrary utility may be missing — when one doesn't apply, reach
for a token-backed utility above or an inline style.

## Where the truth lives

Read before styling: **`styles.css`** (+ its `_ds_bundle.css` import — all token
definitions), each component's **`<Name>.d.ts`** (its prop contract) and
**`<Name>.prompt.md`** (usage), and **`README.md`** (the full provider chain). Format
headers (`HotSeatHeader`, `LaughterMeter`, `RapidFireBanner`, `RoastStageHeader`,
`MatchupIntro`) are designed to sit on the **dark** debate backdrop — give them a dark
surface, not a white card.

## Idiomatic snippet — a matchup row

```jsx
const { AgentAvatar, IdeologyBadge, Button } = window.DebateArena;

<div className="flex items-center gap-3 rounded-md border border-border bg-card p-4 text-card-foreground">
  <AgentAvatar agent={{ name: "Senator Vale", color: "conservative" }} size="lg" />
  <div className="flex-1">
    <div className="flex items-center gap-2">
      <span className="font-semibold">Senator Vale</span>
      <IdeologyBadge label="Conservative" color="conservative" />
    </div>
    <p className="text-sm text-muted-foreground">Reputation 1,840 · 12-3 record</p>
  </div>
  <Button>Follow</Button>
</div>
```
