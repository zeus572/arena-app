# Virtual Candidates — Product Requirements Document (PRD)

> Sub-area of the live Civic Arena platform. Builds on existing production systems: Celebrity Agents (source libraries), Values Profile, Civic Briefings (news triggers), Bot Heartbeat (scheduling), Ranking Engine, and Political Arena debate formats. This is a real implementation — new tables, real API endpoints, real LLM pipeline integration, real persistence. Not a prototype.

---

## 1. Executive Summary

**Virtual Candidates** is a new sub-area of Civic Arena where AI-driven fictional candidates campaign for the next national election in real time. The system covers both Presidential and Congressional races.

Each candidate:

- Has a defined platform, values profile, and source library (Celebrity Agents pattern)
- Publishes campaign posts capped at ~160 characters
- Responds to real-world news events surfaced through Civic Briefings
- Speaks in a range of tones — angry, stern, casual, hopeful, sarcastic, presidential — calibrated per-issue
- Receives granular reactions from signed-in users (thumbs up/down on whole posts and on highlighted fragments)

The feature uses the platform's existing infrastructure rather than duplicating it: the same LLM pipeline that drives Celebrity Agent debates, the same Values Profile axes that Civic Arena already maps users against, and the same news objects (Civic Briefings) that already power the rest of the site.

The product question this feature answers:

> What does a campaign look like when the candidates have to publicly justify their values against real news, and you can react to them line-by-line?

---

## 2. Goals

### Primary

1. Give young users a low-stakes way to observe a full election cycle — not just the horse race, but the daily rhetoric and how candidates respond to events.
2. Make values-to-candidate matching tangible by showing candidate posts alongside the user's own Values Profile.
3. Use granular reactions (fragment-level, not just post-level) to teach users which arguments persuade them and which fall flat.
4. Demonstrate tonal range as a civic literacy concept — the same position can be delivered angrily, calmly, or sarcastically, and those choices matter.

### Secondary

1. Drive return visits by tying candidate posts to current events (the feed always has fresh content).
2. Create a richer signal for the Values Profile — candidate-post reactions are a low-effort way to learn what a user agrees with.
3. Set up future features: straw polls, election night simulation, fundraising mechanics, district-by-district maps.

### Non-goals (for v1)

- Real candidate impersonation. All Virtual Candidates are fictional.
- Real voter registration, donation, or endorsement workflows.
- Polling forecasts or election prediction markets.
- Persuasion mechanics targeting users (no microtargeting).

---

## 3. How This Fits Civic Arena

| Existing system | How Virtual Candidates uses it |
|---|---|
| **Celebrity Agents source libraries** | Each Virtual Candidate has a `SourceLibrary` of platform planks, prior speeches, op-eds, and policy docs (all generated/curated up-front). Posts cite from it. |
| **Values Profile** | Each candidate has a values profile on the same Civic Arena axes. Users see "candidates who match your values" and "candidates who would challenge you." |
| **Civic Briefings** | Briefings are the news triggers. When a briefing publishes, the system decides which candidates respond. |
| **Debate formats** | Candidates can be slotted into existing formats — Tweet Battle, Town Hall, Common Ground, Roast — for periodic cross-candidate exchanges. |
| **Bot Heartbeat** | Extended to schedule candidate posts in addition to debates. |
| **Ranking Engine** | Used to surface posts in the Campaign Feed (relevance + engagement + recency + diversity). |

This means Virtual Candidates is **mostly orchestration** on top of existing systems, not a new platform.

---

## 4. User Stories

- *As a young user*, I open the app and see what AI candidates are saying about today's headlines, so civic news has a personality attached to it.
- *As a curious voter*, I want to compare how five candidates would respond to the same event side-by-side.
- *As a student*, I want to thumbs-up the parts of a candidate's post I agree with and thumbs-down the parts I don't, so I learn what specifically persuades me.
- *As a user with a built Values Profile*, I want to see which candidates match me, which would challenge me, and where I diverge from candidates I broadly support.
- *As a teacher*, I want to assign students to compare candidate responses to a single news event for classroom discussion.
- *As a returning user*, I want to follow specific candidates and get notified when they respond to news I care about.

---

## 5. Core Features

### 5.1 Candidate Roster & Profiles

Each Virtual Candidate has:

- **Identity**: fictional name, fictional party (or independent), office sought, district/state where applicable, avatar with multiple mood states
- **Platform**: 4–8 platform planks, each tagged to issue areas
- **Values Profile**: scores on each Civic Arena axis (Government Role, Liberty/Safety, Expertise, Speech, etc.)
- **Source Library**: platform documents, speech excerpts, position papers, op-eds — all the artifacts a real candidate's communications team would draw on
- **Tone Profile**: default tone + per-issue overrides (e.g., calm on tax policy, fired up on climate)
- **Background**: short bio with backstory, prior career, why they're running
- **Disclaimer**: prominent "AI simulation. Fictional candidate." badge on every appearance

Profile page shows: bio, platform planks, recent posts, values profile visualization, and (eventually) head-to-head match against the user's own Values Profile.

### 5.2 Campaign Feed

The primary surface. A reverse-chronological-by-default stream of candidate posts.

- **Filters**: office (President / Senate / House), party, issue tag, candidate, tone, intensity
- **Sort options**: recent, most-reacted, most-controversial (high mixed reactions), trending
- **Geographic awareness**: signed-in users with a known state/district see their own races elevated
- **News context**: posts triggered by a Civic Briefing show the briefing as a quoted card above the post

### 5.3 Campaign Post

A campaign post is the core content unit. Specs:

- **Character limit**: 160 characters of body text (the asked-for ~160; matches the original SMS-era political-text feel)
- **Optional attachments**: link to a Civic Briefing, link to a platform plank, link to a source library item
- **Tone tag**: one of ~8 tones (see §5.5)
- **Intensity level**: 1–5
- **Issue tags**: 1–3 from a fixed taxonomy
- **Trigger**: news (Civic Briefing ID), platform statement, response to another candidate, or scheduled

Posts are short by design. Depth is in the source library, the platform plank, and (eventually) the debates.

### 5.4 Reaction System

Two layers:

**Whole-post reactions**
- 👍 / 👎 (or "agree" / "disagree" — copy TBD)
- Optional secondary reactions: "well said," "didn't answer," "harsh," "honest" (TBD via user testing)
- Counter visible on each post

**Fragment-level reactions** (the novel mechanic)
- User selects a span of text (highlight by tap-and-hold on mobile, select by drag on desktop)
- Reacts to that span
- Posts accumulate a **reaction heat map** — when viewing a post, users can toggle a view that color-codes each phrase by net sentiment
- Aggregated insights surface on candidate profile: *"Most agreed-with line this week,"* *"Most disagreed-with line this week"*

Why fragments matter: someone may agree with a candidate's diagnosis but disagree with the proposed fix, or vice versa. Whole-post thumbs lose that. Fragment reactions also generate richer Values Profile signal — agreeing with the same 5-word phrase across 20 candidates teaches the system something specific.

### 5.5 Tone & Intensity

**Tone categories** (initial set):

| Tone | Feel |
|---|---|
| Stern | Authoritative, no-jokes, lawyer/general voice |
| Angry / Defiant | Heated, accusatory, "this is unacceptable" |
| Casual / Conversational | "Hey, look, here's the deal" |
| Hopeful / Inspirational | "We can do this together" |
| Sarcastic / Mocking | Dry, ironic, "oh, sure, that'll work" |
| Presidential / Statesmanlike | Measured, gravitas, third-person plural |
| Folksy | Down-to-earth, anecdotal, regional |
| Wonkish | Data-forward, "the latest CBO scoring shows..." |

**Intensity levels** (1–5):

1. Measured
2. Concerned
3. Engaged
4. Heated
5. Fired up

**Visual treatment**:

- Border color and/or thickness on the post card scales with intensity
- Avatar shows a mood expression matching the tone
- Subtle iconography (e.g., a flame icon at intensity 5)
- Optional micro-animation on first render at high intensity

**Per-issue tone mapping**:

Each candidate stores a map of `issue → (tone, intensity)`. So a single candidate can be wonkish-and-measured on healthcare and folksy-and-fired-up on rural broadband. This gives candidates character without locking them into one register.

### 5.6 Cross-Candidate Comparison

A dedicated "Compare" view:

- Pick a Civic Briefing
- See every candidate response side-by-side
- Sort by tone, intensity, party, or reaction-score
- Useful for teachers and engaged users; also the obvious shareable artifact

### 5.7 Match Me with Candidates

Powered by Values Profile.

- Top match
- Closest opponent
- "Surprising agreements" — candidates whose policy positions align with the user's on issues where their values would predict disagreement
- "Productive challenge" — a candidate who shares the user's high-priority value but argues a different conclusion

### 5.8 Candidate-vs-Candidate Exchanges

Re-use existing debate formats:

- **Tweet Battle** (Political Arena v2 format): two candidates trade 280-char (here, ~160-char) jabs over 10 rounds
- **Common Ground**: two ideologically distant candidates find specific overlap
- **Town Hall**: one candidate fielding questions from 3–4 others
- **Roast Battle**: for entertainment, scored on wit

Scheduled by Bot Heartbeat, surfaced in the Campaign Feed as a "debate moment" event.

---

## 6. Presidential vs. Congressional

### Presidential

- Small roster: 4–8 candidates representing distinct values archetypes
- National issues dominate
- Higher post frequency (target: 2–5 posts/candidate/day)
- High share of cross-candidate exchanges
- Always visible to all users regardless of geography

### Congressional

- Larger roster: aim for full Senate and House coverage at scale; v1 covers ~20–40 races as a sample (mix of competitive Senate races and a sample of House districts)
- Mix of national and state/district issues
- Lower post frequency (target: 1–2 posts/candidate/day)
- Geographic filtering critical: a user's home state/district races are elevated; everything else is opt-in
- Senate vs. House framing differs (Senate = state-level, House = district-level)

### Why both at launch

Having only presidential candidates makes the feature feel like a four-person reality show. Having congressional candidates anchors it in the broader machinery of government — and serves Civic Arena's "learn how power works" mission by making clear that not every important race is a presidential one.

---

## 7. Generation Pipeline

Extension of the existing Bot Heartbeat service.

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Trigger Event                                            │
│    • New Civic Briefing published                           │
│    • Scheduled platform-statement slot                      │
│    • Response prompt from another candidate's post          │
│    • Crowd intervention (later phase)                       │
└──────────────┬──────────────────────────────────────────────┘
               ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Candidate Selection                                      │
│    • Match briefing's issue tags against each candidate's   │
│      platform tags                                          │
│    • Apply cooldown (no candidate posts >N times in M hrs)  │
│    • Diversify across parties                               │
│    • For Congress: prefer candidates whose constituency is  │
│      affected                                               │
└──────────────┬──────────────────────────────────────────────┘
               ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. Tone & Intensity Resolution                              │
│    • Look up candidate's (issue → tone, intensity)          │
│    • Add small randomization within candidate's range       │
└──────────────┬──────────────────────────────────────────────┘
               ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. Post Generation (LLM)                                    │
│    • System prompt: candidate persona + source library      │
│      (Celebrity Agents pattern)                             │
│    • Add: tone instruction, intensity instruction,          │
│      "respond to this briefing" instruction                 │
│    • Hard rule: 160 chars max                               │
│    • Required: cite or reference at least one platform      │
│      plank or source library item                           │
└──────────────┬──────────────────────────────────────────────┘
               ▼
┌─────────────────────────────────────────────────────────────┐
│ 5. Enforcement                                              │
│    • If output >160 chars: re-prompt with explicit length   │
│    • If still over: hard-truncate at last sentence boundary │
│    • Strip markdown, emoji-cap (max 2), validate tone tag   │
└──────────────┬──────────────────────────────────────────────┘
               ▼
┌─────────────────────────────────────────────────────────────┐
│ 6. Publish                                                  │
│    • Write to CampaignPost with fragment indices            │
│    • Push to feed                                           │
│    • Apply candidate cooldown                               │
└─────────────────────────────────────────────────────────────┘
```

### Cost controls

- Candidate cooldowns prevent runaway posting
- Per-day budget cap per candidate
- Briefing → candidate matching is rule-based, not LLM-based, to keep costs predictable
- Only the post body is LLM-generated; tone, intensity, issue tags are picked deterministically before the call

---

## 8. Data Model

These types describe persisted entities. Each maps to a database table with an EF migration in the `AddVirtualCandidates` migration set. TypeScript types are shown for cross-stack clarity; the C# entity classes follow the same shape and use enums where the TS types use string unions.

```ts
type Office =
  | { kind: "President" }
  | { kind: "Senate"; state: string }
  | { kind: "House"; state: string; district: number };

type Tone =
  | "stern" | "angry" | "casual" | "hopeful"
  | "sarcastic" | "presidential" | "folksy" | "wonkish";

type Intensity = 1 | 2 | 3 | 4 | 5;

export type VirtualCandidate = {
  id: string;
  slug: string;
  name: string;
  office: Office;
  party: string; // fictional; e.g., "Reform Party", "Common Future"
  isIncumbent: boolean;
  bio: string;
  platformPlanks: PlatformPlank[];
  valuesProfile: ValuesProfile; // same shape as user profile
  defaultTone: Tone;
  defaultIntensity: Intensity;
  issueToneMap: Record<string, { tone: Tone; intensity: Intensity }>;
  sourceLibrary: SourceLibraryItem[];
  avatarBaseUrl: string;
  avatarMoods: Record<Tone, string>; // URL per mood expression
  createdAt: string;
};

export type PlatformPlank = {
  id: string;
  candidateId: string;
  title: string;
  body: string; // full position; can be cited in posts
  issueTags: string[];
};

export type SourceLibraryItem = {
  id: string;
  candidateId: string;
  kind: "speech" | "op-ed" | "policy-doc" | "interview" | "ad" | "town-hall";
  title: string;
  excerpt: string;
  issueTags: string[];
  priority: 1 | 2 | 3; // 1 = core, used in most prompts
};

export type CampaignPost = {
  id: string;
  candidateId: string;
  body: string; // ≤160 chars
  fragments: Fragment[]; // word-or-phrase chunks for reactions
  tone: Tone;
  intensity: Intensity;
  issueTags: string[];
  triggeredBy:
    | { kind: "briefing"; briefingId: string }
    | { kind: "platform" }
    | { kind: "response"; postId: string };
  reactionCounts: { up: number; down: number };
  createdAt: string;
};

export type Fragment = {
  id: string;
  postId: string;
  text: string;
  start: number; // char offset
  end: number;   // char offset
  reactionCounts: { up: number; down: number };
};

export type Reaction = {
  id: string;
  userId: string;
  postId: string;
  fragmentId: string | null; // null = whole-post reaction
  type: "up" | "down";
  createdAt: string;
};

export type ElectionCycle = {
  id: string;
  name: string; // e.g., "2028 General"
  electionDate: string;
  primarySeasonStart: string;
  generalSeasonStart: string;
  offices: Office[];
  candidateIds: string[];
};
```

**Fragment generation**: when a post is published, the system auto-splits the body into fragments at sensible boundaries (clause-level, ~5–15 word spans). Users react to these spans. This avoids requiring the user to define their own span — they tap a pre-defined fragment. A power-user gesture to highlight a custom span can come later.

---

## 9. UX Notes

### Post card anatomy (mobile)

```
┌─────────────────────────────────────────────┐
│  [Avatar]  Maria Velez · Senate · CA        │
│            Common Future · 2h ago           │
│            🔥 Fired up · Stern              │
├─────────────────────────────────────────────┤
│  When ed-tech companies harvest student     │
│  data without consent, that's not           │
│  innovation — it's surveillance.            │
│  Time for a federal floor. #StudentPrivacy  │
├─────────────────────────────────────────────┤
│  ↳ Reacting to: "Congress Advances Student  │
│     Data Privacy Bill"                      │
├─────────────────────────────────────────────┤
│  [👍 142]   [👎 38]   [Heat map] [Compare]  │
└─────────────────────────────────────────────┘
```

### Heat map view

Same post, fragments color-coded by net sentiment (green for positive, gray for neutral, red for negative). Tapping a fragment shows that fragment's react count.

### Trust signals everywhere

- "AI simulation. Fictional candidate." badge on every avatar
- "Generated [time], based on [briefing title]" stamp on each post
- "View source library" link on candidate profile

### Empty states

- No candidate posts today: show a candidate's most-reacted post of the week
- No briefings to react to: candidates fall back to scheduled platform statements

---

## 10. Interactions With Existing Features

### Values Profile
- Reactions to candidate post fragments feed back into the user's Values Profile (low weight, high frequency)
- "Match Me" surfaces top candidates
- Pressure-test scenarios can use specific candidate posts as stimuli

### Civic Briefings
- Briefing detail pages get a "Candidate Reactions" sidebar showing the most-reacted candidate posts on that briefing
- "Compare candidates on this story" CTA

### Celebrity Agents
- Source library implementation is shared
- Celebrity agents are *not* presented as candidates — they remain in the debate area
- However: a future feature could let a celebrity agent "endorse" or "criticize" a Virtual Candidate as a debate moment

### Concepts / Quizzes
- Candidate posts can be used as quiz stems: "What civic concept does this candidate's position reference?"
- "Fact, interpretation, prediction, or value?" quizzes work especially well on short candidate posts

### Debate Formats
- All seven Political Arena v2 formats apply
- Tweet Battle is the most natural fit
- Town Hall lets users (and other candidates) ask questions

---

## 11. MVP Phasing

### MVP 1 — Presidential only, whole-post reactions
- DB migration: VirtualCandidate, PlatformPlank, CandidateSource, CampaignPost, PostReaction, ElectionCycle
- ClaudeLlmService extended with GenerateCampaignPostAsync; 160-char enforcement
- BotHeartbeatService extended with NewBriefingPublished and ScheduledPlatformStatement triggers
- Per-candidate daily LLM budget + cooldowns wired in
- 4–6 presidential candidates seeded with full source libraries
- Campaign Feed API + page (reverse chronological, filter by candidate)
- News-triggered posts (2–5 per candidate per day)
- Whole-post 👍 / 👎 with idempotent reaction writes
- Three tones to start: calm / engaged / fired-up (simplified from full set)
- Candidate profile page
- Admin dashboard for spend + post counts
- Disclaimer on every candidate appearance

**Success**: feed is fresh, posts are coherent, reactions accumulate, no real-person confusion.

### MVP 2 — Fragment reactions + full tone system
- Auto-fragmenting posts
- Per-fragment reactions
- Heat map view on posts
- Full 8-tone palette with avatar mood states
- Per-issue tone mapping
- "Most-loved / most-disliked line" insights on profile

### MVP 3 — Congressional + geographic filtering
- ~20 Senate candidates (10 competitive races, both sides)
- ~20 House candidates across selected districts
- Home state/district elevation for signed-in users
- Geographic filter in Campaign Feed
- Compare view by office

### MVP 4 — Match Me + Values Profile integration
- "Candidates similar to you" page
- "Productive challenge" recommendation
- Reaction signal feeding back into Values Profile (low weight)

### MVP 5 — Cross-candidate debates
- Schedule Tweet Battles, Town Halls, and Common Ground events
- Surface as feed events
- Replay archive

### MVP 6 (later) — Engagement loops
- Follow candidates → notifications
- Weekly "campaign digest"
- Election countdown
- Straw poll (educational, clearly fictional)

---

## 12. API Surface

New endpoints added to the Civic Arena API. Follow existing conventions for auth, pagination, error envelopes, and rate limiting.

### Read endpoints (public, optionally personalized for signed-in users)

```
GET  /api/candidates
       ?office=President|Senate|House
       &party=<slug>
       &state=<US-state-code>
       &district=<int>
       &cursor=<opaque>&limit=<int>

GET  /api/candidates/:slug
GET  /api/candidates/:slug/posts?cursor=<opaque>&limit=<int>
GET  /api/candidates/:slug/sources           # source library
GET  /api/candidates/:slug/values            # values profile
GET  /api/candidates/:slug/platform          # platform planks

GET  /api/campaign/feed
       ?cursor=<opaque>&limit=<int>
       &office=...&party=...&state=...&district=...
       &tone=...&minIntensity=...&issue=...
       &sort=recent|top|controversial|trending

GET  /api/posts/:id                          # includes fragments + aggregate reactions
GET  /api/posts/:id/heatmap                  # fragment-level reaction aggregates
GET  /api/briefings/:id/candidate-reactions  # posts triggered by a briefing
GET  /api/election/cycles/current
GET  /api/election/races                     # filter by office/state/district
```

### Write endpoints (signed-in users)

```
POST   /api/posts/:id/reactions
         body: { type: "up" | "down" }
DELETE /api/posts/:id/reactions               # remove the user's own reaction

POST   /api/posts/:id/fragments/:fragmentId/reactions
         body: { type: "up" | "down" }
DELETE /api/posts/:id/fragments/:fragmentId/reactions

POST   /api/candidates/:slug/follow
DELETE /api/candidates/:slug/follow
POST   /api/candidates/:slug/mute
DELETE /api/candidates/:slug/mute
```

### Personalization (signed-in users with a Values Profile)

```
GET  /api/me/candidate-matches
       returns: { topMatches: Candidate[], productiveChallenges: Candidate[],
                  surprisingAgreements: Candidate[] }

GET  /api/me/campaign-feed                   # feed with personalization signals applied
```

### Internal / admin endpoints

```
POST /api/admin/candidates                   # create / update candidate
POST /api/admin/candidates/:slug/sources     # add source library item
POST /api/admin/candidates/:slug/posts/generate
       body: { triggerBriefingId?: string, force?: boolean }
POST /api/admin/election/cycles
GET  /api/admin/budget                       # per-candidate LLM spend, post counts
```

Reaction writes are idempotent per `(userId, postId, fragmentId?)`. The server enforces the 160-character body limit and rejects malformed fragment ranges. All reaction endpoints update aggregate counters atomically; reaction counts are eventually consistent in feed responses but strongly consistent on the post detail endpoint.

---

## 13. Routes (frontend)

```
/candidates                       Index / Campaign Feed
/candidates/:slug                 Candidate profile
/candidates/:slug/posts           Candidate's post archive
/candidates/compare               Compare view (multi-select)
/posts/:id                        Single post with heat map
/election                         Election cycle overview
/election/president               Presidential race hub
/election/senate                  Senate race hub (filtered by user state)
/election/house                   House race hub (filtered by user district)
/match                            Match Me with Candidates (requires Values Profile)
```

---

## 14. Tone Prompt Snippets (illustrative)

Tone instructions injected into the LLM prompt before generation. Sketches:

**Angry / Defiant, intensity 5:**
> Write with controlled outrage. Short, declarative sentences. Name the wrong directly. One exclamation point maximum. No hedging language ("perhaps," "might," "could be considered"). End on a call to action.

**Folksy, intensity 2:**
> Conversational. First-person plural ("we," "us") preferred. One concrete image — a porch, a kitchen table, a small town, a family member. Avoid policy jargon entirely. End on a question or invitation.

**Wonkish, intensity 3:**
> Lead with the specific data point or source. One technical term, defined inline. Acknowledge the tradeoff explicitly. End with the policy mechanism, not the outcome.

**Sarcastic, intensity 4:**
> Dry. Reference the opposing position as if it were obviously absurd, without ever calling it absurd. Use the word "apparently" or "remarkable" once. No name-calling. End on the unspoken implication.

The post-generation enforcer also validates that the output reads in the expected register (lightweight classifier or rules; v1 could skip this and trust the prompt).

---

## 15. Safety & Trust

- **No real-candidate impersonation.** Fictional names, fictional parties. Avatars stylized, not photorealistic. Disclaimers on every post and profile.
- **Election integrity.** No straw poll results presented as predictive. No "vote here" calls to action that could be mistaken for real registration or voting.
- **Tone calibration.** Even at intensity 5, posts cannot endorse violence, target real individuals, or contain slurs. System prompt has hard rules. Output is screened.
- **Bias balance.** Roster designed for ideological diversity across Civic Arena's axes, not just left/right. Candidates explicitly span Liberty Guardian, Public Builder, Local Steward, Future Investor, etc. archetypes from the Values Profile spec.
- **Child safety.** Per Civic Arena's youth focus: no profanity, no sexualized content, no targeted personal attacks even in Roast format.
- **Source transparency.** Every post is traceable to a candidate's source library or platform plank.
- **User control.** Users can mute candidates, mute the whole feature, or filter by tone if they find high-intensity posts distressing.

---

## 16. Open Questions

1. **Tied to a real election cycle or perpetual?** Real cycles give natural urgency and a clear election date. Perpetual cycles avoid tying us to real-world events. **Recommendation**: fictional election cycle dated ~12–18 months out, refreshed annually.
2. **Should congressional candidates appear in races against each other, or solo?** A real congressional race has 2–4 candidates per seat. Modeling all of them is content-heavy. **Recommendation**: for MVP 3, 2 candidates per modeled seat (one major-party-equivalent each); expand later.
3. **Avatar treatment.** Illustrated, photographic-style, or abstract? Photographic styles risk real-person confusion. **Recommendation**: stylized illustration with clear "fictional" affordance.
4. **Reaction copy.** "Thumbs up / down" is universal but politically loaded. Alternatives: "agree / disagree," "yes / no," "with me / not me." Test in user research.
5. **Fragment granularity.** Auto-fragment at clause boundaries vs. let users highlight arbitrary spans. **Recommendation**: auto-fragment for v1; revisit after watching how users actually interact.
6. **Cross-feature reactions.** Should reactions on candidate posts modify the user's Values Profile? At what weight? **Recommendation**: yes, but low weight; high frequency over time produces a useful signal without making one snarky reaction dominate.
7. **Moderator role.** Do candidates ever post "moderator commentary" on each other? Or is that strictly the Political Arena commentary system? **Recommendation**: keep moderator commentary in the debate layer, not the campaign feed.
8. **Endorsements from Celebrity Agents.** Should Lincoln "endorse" or "criticize" Virtual Candidates as a debate moment? Entertaining and pedagogically interesting, but risks blurring fictional/historical/celebrity lines. **Recommendation**: defer past MVP.

---

## 17. Success Criteria

The feature is successful if:

- New posts appear in the Campaign Feed regularly enough that returning users see fresh content (target: 80% of returning users see ≥3 new posts since last visit)
- Median post receives ≥5 reactions within 24h
- Fragment reactions are used on ≥20% of reacted posts (signal that the feature is more than a vanity layer)
- "Match Me" page sees ≥30% of users with a Values Profile clicking through to a candidate profile
- Cross-candidate debates generate higher engagement than equivalent debates in the standalone debate area (signal that candidate identity adds value)
- Users can articulate, after a session, what one candidate believes and what tone they take on a specific issue (qualitative research signal)
- No incidents of users confusing Virtual Candidates with real candidates
- Teachers find the comparison view classroom-usable

---

## 18. Risks

| Risk | Mitigation |
|---|---|
| Users mistake fictional candidates for real ones | Visible disclaimers, stylized avatars, fictional party names |
| Intensity-5 angry posts feel inflammatory and drive negative engagement | Cap intensity-5 frequency per candidate per day; review tone prompts carefully |
| LLM cost runs away with many candidates posting frequently | Per-candidate daily budget; rule-based candidate selection (not LLM-based); cooldowns |
| Posts drift off-character over time | Source library citation requirement; character anchor in every prompt (Political Arena v2 pattern) |
| Reaction-driven feedback loop pulls posts toward extremity | Don't show real-time reaction counts during candidate prompt construction; tone/intensity is set by the system, not by the post's reception |
| Echo chamber via Match Me | Always show both "matches you" and "challenges you" candidates; explicit copy on this |
| Real-world election content gets confused with Virtual Candidate content | Strict visual differentiation; never use real candidate names; clear nav separation between Civic Briefings (real news) and Campaign Feed (fictional reactions) |
| Tone palette feels mocking of real political speech patterns | Avoid caricature in tone prompts; tone is about register, not stereotype |

---

## 19. What This Doesn't Solve

This PRD does not address:

- Real-time event detection beyond Civic Briefings (the system reacts only to what the Briefing layer surfaces)
- Multi-modal content: posts are text-only in v1; video/image candidate content is a later question
- Adversarial users trying to game fragment reactions (rate limits and abuse detection are the standard answer; out of scope here)
- Internationalization or non-US elections
- Down-ballot races (state legislature, mayor, school board) — viable later, but the data and content effort scales fast

These are intentional v1 boundaries, not oversights.

---

## 20. Implementation Prompt for Claude / Codex

```
You are helping implement Virtual Candidates, a new sub-area of the live Civic Arena
platform. This is a production feature, not a prototype — wire it into the existing
backend, persistence, LLM pipeline, and ranking systems.

Reuse, do not duplicate:
- The Celebrity Agents source library implementation (each candidate has a real
  SourceLibrary backed by AgentSources or an equivalent table)
- The ClaudeLlmService pipeline, including the search_agent_sources tool pattern,
  for candidate post generation
- The BotHeartbeatService for scheduling and dispatching post generation
- The Civic Briefings content type as the news trigger surface
- The RankingService for ordering the Campaign Feed
- The Values Profile axes and scoring engine for candidate-user matching
- The existing reaction infrastructure where reasonable; extend it to support
  fragment-level reactions

Build (across backend, API, and frontend):

Backend / data layer
- New tables: VirtualCandidate, PlatformPlank, CandidateSource, CampaignPost,
  PostFragment, PostReaction, ElectionCycle, CandidateRace, CandidateFollow,
  CandidateMute
- EF migration: AddVirtualCandidates
- Per-candidate daily LLM budget tracking and enforcement
- Cooldown logic so no candidate posts more than N times per M hours
- Fragment auto-generation at clause boundaries when a post is published
- Aggregate reaction counters updated atomically on each reaction write

LLM pipeline
- Extend ClaudeLlmService with a GenerateCampaignPostAsync method
- System prompt includes: candidate persona, source library (priority 1 + topic-
  matched), tone instruction, intensity instruction, 160-char hard rule,
  disclaimer behavior block
- Post-generation enforcement: re-prompt if over 160 chars; hard-truncate on
  second failure
- Tool support: search_agent_sources scoped to the candidate's library
- Required: each post must reference a platform plank or source library item

Scheduling
- Extend BotHeartbeatService with two new triggers:
  (1) NewBriefingPublished → fan out to matching candidates
  (2) ScheduledPlatformStatement → for periods between news events
- Rule-based candidate selection (not LLM-based) to keep cost predictable

API
- All endpoints under §12 of the PRD, following existing Civic Arena conventions
  for auth, pagination, error envelopes, and rate limiting
- Idempotent reaction writes per (userId, postId, fragmentId?)
- Personalized endpoints (/api/me/*) require auth and a Values Profile

Frontend
- Campaign Feed page (/candidates)
- Candidate Profile page (/candidates/:slug)
- Post detail page with fragment heat map (/posts/:id)
- Compare view (/candidates/compare)
- Match Me page (/match)
- Election hub pages (/election/*)
- Post card component with tone/intensity visual treatment (border color, avatar
  mood state, optional icon)
- Fragment reaction interaction: tap a pre-fragmented span to react
- "AI simulation. Fictional candidate." badge on every candidate appearance

Safety and ops
- Output screening: no slurs, no targeted attacks on real individuals, no
  endorsements of violence, no calls to register/vote that could be mistaken
  for real-election guidance
- Admin dashboard for per-candidate spend, post counts, and reaction trends
- Mute and follow controls for users
- Hard limit on intensity-5 posts per candidate per day

Tone should match the rest of Civic Arena: curious, balanced, non-judgmental,
youth-friendly without being childish. Even at intensity 5, posts should be
sharp without being cruel.

All candidates, parties, and election cycle dates are fictional. Stylized avatars
only — no photorealistic likenesses. The Civic Briefings layer remains the real-
news surface; the Campaign Feed is clearly the fictional-reaction surface, and
the two must be visually and navigationally distinct.
```

---
