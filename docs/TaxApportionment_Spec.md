# Civic Arena — Tax Apportionment Explainer

**Feature spec for Claude Code · production build**

---

## 0. Summary

A new Civic Arena module: an interactive explainer titled **"Who Gets Your Tax Dollar?"** that proves, then personalizes, the gap between federal and state-and-local tax burden. The civic payload sits at the end: once a user sees how much of their tax flows to Washington versus stays local, they're equipped to reason about what counts as "pork" and whether the current allocation is right.

This fits the Civic Arena thesis directly — it descends from a values-level argument ("pork is wasteful federal spending") to a governable, factual question ("how is tax burden actually apportioned between levels of government, and is that the right split?").

This spec targets the **live backend, real persistence, and the existing Civic Arena frontend** — not a prototype. All tax parameters below are real 2025–2026 figures, already researched and verified; treat the numbers in §3 as the source of truth and do not substitute model priors.

### Build phases (gated)
- **Phase 1** — Tax engine (pure functions, fully unit-tested). **Gate: all golden-value tests in §6 pass.**
- **Phase 2** — State profile data layer + API endpoint.
- **Phase 3** — Frontend module (calculator, scaling table, state cards, ponder section).
- **Phase 4** — Integration into Civic Briefings / Values Profile, telemetry.

Do not advance a phase until the prior phase's gate passes.

---

## 1. Where it lives in Civic Arena

- Surface as a **Civic Briefing** of a new type `interactive_model` (extends the existing briefing system rather than a one-off page).
- Reachable from the briefings feed and linkable as a standalone route: `/briefings/who-gets-your-tax-dollar`.
- The closing "ponder" questions should feed the **Values Profile** system as `issue_specific` civic questions (federalism / fiscal axes). See §7.
- Tone and framing follow the existing youth-first, nonpartisan Civic Arena voice: no party labels, present the arithmetic as settled and the judgment as open.

---

## 2. Architecture

```
TaxApportionment/
├── engine/
│   ├── federal.ts        # pure functions, 2025 federal law
│   ├── state.ts          # pure functions, parameterized per state
│   ├── types.ts
│   └── engine.test.ts    # golden-value tests (Phase 1 gate)
├── data/
│   └── stateProfiles.ts  # real per-state parameters (§4)
├── api/
│   └── taxModel.controller.*   # GET endpoints (§5)
└── ui/
    ├── TaxApportionment.tsx     # container
    ├── HouseholdCalculator.tsx
    ├── SplitBar.tsx
    ├── ScalingTable.tsx
    ├── StateCard.tsx
    ├── CaveatGrid.tsx
    └── PonderSection.tsx
```

**Principle (matches existing Civic Arena LLM-efficiency pattern):** the tax engine is **deterministic and LLM-free**. No model calls at compute time. All math is closed-form. An LLM may optionally be used *offline* to draft the per-state `notes` prose (§4), but those strings are stored, not generated on demand.

---

## 3. Federal tax engine — source of truth

Computed exactly. These are 2025 figures (filed in 2026) unless noted.

### 3.1 Ordinary income brackets (2025, IRS Rev. Proc. 2024-40)

Seven rates: 10, 12, 22, 24, 32, 35, 37%.

**Single filer** — lower bound of each bracket:

| Rate | Taxable income ≥ |
|---|---|
| 10% | $0 |
| 12% | $11,925 |
| 22% | $48,475 |
| 24% | $103,350 |
| 32% | $197,300 |
| 35% | $250,525 |
| 37% | $626,350 |

**Married filing jointly** — lower bound of each bracket:

| Rate | Taxable income ≥ |
|---|---|
| 10% | $0 |
| 12% | $23,850 |
| 22% | $96,950 |
| 24% | $206,700 |
| 32% | $394,600 |
| 35% | $501,050 |
| 37% | $751,600 |

Brackets are **marginal** — each rate applies only to income within its band.

### 3.2 Standard deduction (2025, OBBBA)

| Filing | Standard deduction |
|---|---|
| Single | $15,750 |
| Married filing jointly | $31,500 |

Taxable income = `max(0, gross income − standard deduction)`. (Itemization, credits, and above-the-line adjustments are out of scope for v1 — see §8.)

### 3.3 FICA / payroll

| Component | Rate | Cap |
|---|---|---|
| Social Security (OASDI) | 6.2% | Applies to first **$176,100** of wages (2025 wage base) |
| Medicare (HI) | 1.45% | No cap |
| Additional Medicare | 0.9% | On wages over **$200,000** (single) / **$250,000** (MFJ); employee-only, no employer match |

Show **only the employee share** in the headline figure. Note in the UI that the employer matches 6.2% + 1.45% (7.65%) invisibly, and that economists attribute much of it to wages — counting it would raise the federal share further. (2026 note: SS wage base rises to $184,500; keep the wage base a named constant so it's a one-line update.)

### 3.4 Federal calculation

```
taxableIncome   = max(0, income − STD_DED[filing])
incomeTax       = progressive(taxableIncome, BRACKETS[filing])
socialSecurity  = min(income, SS_WAGE_BASE) × 0.062
medicare        = income × 0.0145
addlMedicare    = max(0, income − ADDL_MED_THRESHOLD[filing]) × 0.009
fica            = socialSecurity + medicare + addlMedicare
federalTotal    = incomeTax + fica
```

---

## 4. State tax engine — source of truth

The state layer is **parameterized**, not hardcoded per income. Each state profile carries an income-tax rule, a sales-tax rule, and a property-tax rule, plus a prose `notes` string. v1 ships **8 representative states** spanning the full structural range; the model is built to scale to all 50 (see §8).

### 4.1 State profile shape

```ts
type IncomeRule =
  | { type: "none" }
  | { type: "flat"; rate: number; stdDed: number }
  | { type: "progressive"; brackets: [number, number][]; stdDed: number };

interface StateProfile {
  code: string;            // "CA"
  name: string;            // "California"
  glyph: string;           // emoji used as a quiet marker, not decoration
  income: IncomeRule;      // single-filer schedule
  salesRate: number;       // combined avg state+local
  consumptionShare: number;// taxable consumption as a fraction of income (default 0.40)
  propRate: number;        // effective property tax rate
  homeMultiple: number;    // imputed home value as a multiple of income
  notes: string;           // stored prose; what makes this state unusual
}
```

### 4.2 State computation

```
stateIncomeTax  = byRule(income, profile.income)        // none → 0; flat → (income−stdDed)×rate; progressive → bands
salesTax        = income × profile.consumptionShare × profile.salesRate
propertyTax     = income × profile.homeMultiple × profile.propRate
stateTotal      = stateIncomeTax + salesTax + propertyTax
```

### 4.3 v1 state data (verified 2025–2026)

> These are real parameters. Single-filer income schedules are simplified to top-line brackets; sales rates are combined state+local averages; property rates are state effective averages. `consumptionShare = 0.40` across all states in v1 (flagged as an assumption in the UI).

| Code | Name | Income tax | Avg combined sales | Eff. property rate | Imputed home (× income) |
|---|---|---|---|---|---|
| CA | California | Progressive, 1%–13.3% (incl. 1% MHS surcharge >$1M) | 8.82% | 0.68% | 4.5× |
| NY | New York | Progressive, 4%–10.9% | 8.53% | 1.40% | 3.5× |
| TX | Texas | None | 8.20% | 1.58% | 3.2× |
| FL | Florida | None | 7.00% | 0.91% | 3.6× |
| WA | Washington | None (7% cap-gains tax >$262k only) | 9.38% | 0.87% | 4.2× |
| CO | Colorado | Flat 4.40% | 7.81% | 0.49% | 4.8× |
| PA | Pennsylvania | Flat 3.07%, no std. deduction | 6.34% | 1.49% | 2.9× |
| IL | Illinois | Flat 4.95%, no std. deduction | 8.86% | 1.88% | 3.0× |

**Income brackets to encode (single filer, top-line; lower bound → rate):**

- **CA progressive:** 0→1%, 10,756→2%, 25,499→4%, 40,245→6%, 55,866→8%, 70,606→9.3%, 360,659→10.3%, 432,787→11.3%, 721,314→12.3%; +1% Mental Health Services surcharge over $1M (top effective 13.3%). State std. deduction ≈ $5,540.
- **NY progressive:** 0→4%, 8,500→4.5%, 11,700→5.25%, 80,650→5.5%, 215,400→6%, 1,077,550→6.85%, 5,000,000→9.65%, 25,000,000→10.9%. State std. deduction ≈ $8,000.
- **CO flat:** 4.40% on federal taxable income (std. deduction mirrors federal $15,750).
- **PA flat:** 3.07%, std. deduction $0 (first dollar taxed).
- **IL flat:** 4.95%, std. deduction $0 (small personal exemption omitted in v1).
- **TX / FL / WA:** `{ type: "none" }`.

**Stored `notes` prose (per state):**

- **CA:** "Top 13.3% rate includes a 1% Mental Health Services surcharge over $1M. No tax on Social Security benefits. Prop. 13 caps assessed-value growth, so effective rates on long-held homes run lower."
- **NY:** "NYC and Yonkers stack a local income tax on top of the state rate — not modeled here, so a city resident's burden is higher. High property tax, especially outside NYC."
- **TX:** "No personal income tax. The state leans on some of the nation's highest property taxes and a high combined sales tax to fund itself."
- **FL:** "No personal income tax. Homestead exemption and a 'Save Our Homes' assessment cap soften property tax for primary residents. Tourism shifts some sales-tax burden onto visitors."
- **WA:** "No wage income tax, but a 7% capital-gains tax applies above ~$262k of gains (not modeled — this calculator assumes wage income). Among the highest combined sales-tax rates in the country."
- **CO:** "Flat 4.4% on federal taxable income. Low effective property tax rate, but high home values raise the dollar amount."
- **PA:** "Flat 3.07% with no standard deduction — the first dollar is taxed. Many municipalities add a local earned-income tax (~1%), not modeled here."
- **IL:** "Flat 4.95%, no standard deduction (a small personal exemption applies instead, omitted here). Among the highest effective property tax rates in the nation. Retirement income is exempt."

---

## 5. API

Stateless, cacheable, LLM-free.

```
GET /api/tax-model/states
  → [{ code, name, glyph, incomeSummary, salesRate, propRate, notes }]   // for state pickers + cards

GET /api/tax-model/compute?income=100000&filing=single&state=CA
  → {
      income, filing, state,
      federal: { incomeTax, socialSecurity, medicare, addlMedicare, fica, total, effectiveRate },
      stateLocal: { incomeTax, salesTax, propertyTax, total, effectiveRate },
      combined: { total, effectiveRate, federalShare, stateShare }
    }

GET /api/tax-model/ladder?filing=single&state=CA
  → rows for the scaling table at preset incomes [30k, 60k, 100k, 175k, 350k, 750k]
```

Compute can also run client-side from the shipped engine; the endpoint exists for server-rendered briefings and to keep one canonical implementation. If client and server both compute, they must import the **same** engine module.

---

## 6. Phase 1 gate — golden-value tests

The engine must reproduce these (rounded to nearest dollar). Compute by hand / spreadsheet to confirm before wiring UI. **These are the acceptance gate — do not proceed to Phase 2 until they pass.**

Federal, **single filer**:

| Income | Taxable (after $15,750) | Income tax | SS (6.2% cap $176,100) | Medicare 1.45% | Addl Med 0.9% | FICA | Federal total |
|---|---|---|---|---|---|---|---|
| $30,000 | $14,250 | $1,471.50 | $1,860.00 | $435.00 | $0 | $2,295.00 | $3,766.50 |
| $60,000 | $44,250 | $5,065.50 | $3,720.00 | $870.00 | $0 | $4,590.00 | $9,655.50 |
| $100,000 | $84,250 | $13,966.50 | $6,200.00 | $1,450.00 | $0 | $7,650.00 | $21,616.50 |
| $250,000 | $234,250 | $51,648.50 | $10,918.20 | $3,625.00 | $450.00 | $14,993.20 | $66,641.70 |

Spot-check rule: at $250k single, addl Medicare = (250,000 − 200,000) × 0.9% = $450; SS is capped at 176,100 × 6.2% = $10,918.20. If your engine produces these four rows exactly, the federal bracket walk and FICA caps are correct.

State spot-checks (income $100,000, single, using v1 params):

- **TX:** incomeTax = 0; salesTax = 100,000 × 0.40 × 0.0820 = $3,280; propertyTax = 100,000 × 3.2 × 0.0158 = $5,056; stateTotal = $8,336.
- **CA:** salesTax = 100,000 × 0.40 × 0.0882 = $3,528; propertyTax = 100,000 × 4.5 × 0.0068 = $3,060; incomeTax = progressive over CA brackets on (100,000 − 5,540).
- **PA:** incomeTax = 100,000 × 0.0307 = $3,070 (no std. deduction).

Add a test asserting `federal + state` and `federalShare + stateShare ≈ 1.0`.

---

## 7. Frontend module

Six sections, top to bottom. The design is already built and validated as a reference artifact (editorial/civic treatment: warm paper `#f6f2ea`, ink `#1a1714`, federal blue `#1d4e89`, state terracotta `#b5552d`, Georgia serif display + Helvetica utility). **Reuse Civic Arena's existing design tokens and component library** where they exist; the colors above are the intended semantic mapping (federal = blue, state/local = terracotta) and should be applied through the design system, not hardcoded if tokens already cover them.

1. **Hero** — "Who actually gets your tax dollar?" Sets up the "pork" framing and the two-level question.
2. **Macro proof** — three stat cards: **$5.07T** federal collections (IRS/USAFacts FY2024), **~$2.1T** state+local tax revenue (Census 2024), **≈2.4×** ratio. One paragraph: the gap is why federal money appears everywhere; "pork" is a judgment on top of arithmetic.
3. **Household calculator** — income slider ($20k–$800k), filing toggle (single / MFJ), state chips. Live **split bar** (federal blue vs. state terracotta) + two breakdown columns showing every line item with a one-line plain-language note and the effective rate.
4. **Scaling table** — federal vs. state vs. federal-share across the six preset incomes for the selected state/filing, with a mini split-bar per row. Highlight the row matching the calculator's current income. Caption: federal share rises with income because federal income tax is steeply progressive while sales/property taxes are flat-to-regressive; at low incomes state/local can be the larger share.
5. **State card** — the selected state's income/sales/property headline stats + the stored `notes` prose.
6. **Caveats ("Where the model gets fuzzy")** — six cards, content fixed below.
7. **Ponder** — dark section, four questions (below). These are the civic payoff and the hook into the Values Profile.

### Caveat content (ship verbatim)
- **Consumption — sales tax is an estimate:** assumes a household spends a taxable 40% of income; real spending varies with income, and groceries/services/rent are often exempt.
- **Housing — property tax assumes you own:** imputes a home worth a multiple of income; renters pay indirectly through rent, so this overstates the renter burden.
- **Local — city taxes mostly excluded:** NYC, Yonkers, and many PA/OH municipalities levy their own income taxes on top of the state.
- **Capital — investment income ignored:** model treats all income as wages; capital gains face different federal rates, escape FICA, and are taxed differently by states.
- **Behavior — caps and credits simplified:** SALT cap, child tax credit, EITC, retirement exclusions, homestead exemptions all move real bills; omitted for legibility.
- **Employer FICA — only your half shown:** employer's matching 7.65% never appears on the stub; counting it would raise the federal share.

### Ponder questions (ship verbatim; also seed as Values Profile `issue_specific` items)
- If Washington collects most of the money, should it also decide most of how it's spent — or send more back with no strings?
- A bridge in one state is funded by taxpayers in all fifty. When is that fair burden-sharing, and when is it one state's project on everyone's bill?
- Low earners often pay a larger *share* to state and local government. Does that change who should fund what?
- Would you trade a lower federal tax for a higher state one, keeping more decisions local — even if your state raises less overall?

---

## 8. Explicitly deferred (do not build in v1)

- Remaining 42 states (engine supports them; data is the work — same manual-curation note as the Celebrity Agents source libraries).
- Capital-gains / investment-income toggle (WA's regime makes this the highest-value next addition).
- Itemized deductions, SALT cap interaction, child tax credit, EITC.
- Local (city/municipal) income taxes.
- Renter vs. owner toggle for property tax.
- User-saved scenarios / sharing.

---

## 9. Data provenance

All figures verified for tax year 2025 (filed 2026). Keep wage base, brackets, standard deduction, and per-state rates as named constants with a `// verified 2025-2026` comment and a single `TAX_YEAR` constant, so the annual refresh is a contained edit.

- **Federal brackets / standard deduction:** IRS Rev. Proc. 2024-40; OBBBA (July 2025) standard deduction; Tax Foundation 2025 bracket tables.
- **FICA:** Social Security Administration (2025 wage base $176,100; 2026 $184,500); Medicare + 0.9% additional Medicare thresholds.
- **Federal vs. state/local revenue:** IRS / USAFacts FY2024 ($5.07T federal); U.S. Census Bureau state & local tax revenue 2024.
- **State income / sales / property rates:** Tax Foundation 2025 state income tax brackets, combined sales tax rates, and effective property tax rates.

Surface a short source line in the briefing footer (the existing Civic Briefing source-transparency block).

---

## 10. Definition of done

- [ ] Engine reproduces every golden value in §6 exactly (Phase 1 gate).
- [ ] `compute` and `ladder` endpoints return correct shapes; client and server share one engine module.
- [ ] All 8 states selectable; cards render stored notes; no live LLM calls in the compute path.
- [ ] Calculator, split bar, scaling table, state card, caveats, and ponder render and are responsive to mobile.
- [ ] Federal = blue, state/local = terracotta applied via design system; keyboard focus visible; reduced-motion respected.
- [ ] Ponder questions seeded into the Values Profile as `issue_specific` questions on the federalism/fiscal axes.
- [ ] Briefing footer carries the source-transparency line.
