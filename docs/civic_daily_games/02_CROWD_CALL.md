# 02 — Crowd Call

**Guess what share of people got each question right.** Five rounds, ~60 seconds.
Read `00_OVERVIEW.md` first. **This is the one to build first.**

## The loop

1. You're shown a civics question and the answer — not asked to answer it yourself.
2. You guess: what percentage of people got this right? Slider, 0–100.
3. Reveal: the true share, your error, and a one-line explanation of the question.
4. After five rounds: a total score out of 100 and a summary line —
   *"You overestimated how divided people are on 3 of 5."*

The skill being trained is **calibration**: noticing that the country does not think what
your feed says it thinks. That is Civersify's thesis stated as a game, which makes this the
most on-brand of the six.

## Why it ships first

Zero new content pipeline and zero authoring. `QuizQuestion` rows already exist,
`QuizResponse` already stores every answer, and `QuizController` already computes a 60-day
moving average of the correct rate. Every quiz question in the bank becomes game content the
day this ships. It is the fastest possible read on whether daily-game traffic converts.

## Required refactor first

`QuizController.PollStatsAsync` is private, and `PollWindowDays = 60` lives on the controller.
Extract to `backend-civic/Services/QuizPollStats.cs`:

```csharp
public class QuizPollStats
{
    public const int WindowDays = 60;
    public Task<Dictionary<Guid,(int Total,int Correct)>> ForQuestionsAsync(
        IEnumerable<Guid>? questionIds, CancellationToken ct = default);
}
```

`QuizController` calls it too. Do not duplicate the window logic — one source of truth for
the 60-day figure, or the game and the quiz page will disagree with each other in public.

## The cold-start problem — read this before building

Crowd Call's data is *our own users' answers*. At low traffic that data is thin, and thin
data makes the game both wrong and boring. This matters because the entire point of these
games is to *attract* users — a game that needs users in order to work is circular.

Two mitigations, both required:

**1. Minimum sample.** A question is only eligible when it has **≥ 50 responses** in the
60-day window. Below that it is excluded from selection.

**2. Seed with published polling.** Ship `Seed/crowd-call-polls.json` — roughly 40 authored
items drawn from published national polls (Pew, Gallup, AP-NORC), each carrying the true
figure, the fielding date, the sample size, and a source URL. These work at zero traffic and
are strictly better content anyway: "what share of Americans can name all three branches"
is a more interesting question than "what share of our users got question 7 right."

The payload therefore carries a `crowdSource` discriminator, and the reveal must always
name it. Attributing a Pew figure to "our users" — or vice versa — is a credibility
problem, not a cosmetic one.

## Content source

| `crowdSource` | Source | True value | Refresh |
|---|---|---|---|
| `civic-users` | `QuizResponse` via `QuizPollStats` | Correct rate, 60-day window | Continuous |
| `national-poll` | `Seed/crowd-call-polls.json` | Published figure | Manual, with fielding date |

Selection per puzzle: five items, mixed across both sources, no item reused within 30 days.
Set `SourceNewsItemId` when the underlying quiz question came from a news item
(`QuizQuestion.SourceNewsItemId`), for provenance.

## Payload contract (`PayloadVersion = 1`)

```jsonc
{
  "rounds": [
    {
      "prompt": "Which branch can declare a law unconstitutional?",
      "answer": "The judicial branch",
      "explanation": "Judicial review was established in Marbury v. Madison (1803).",
      "crowdSource": "civic-users",
      "attribution": "Civersify players, last 60 days",
      "sampleSize": 412,
      "trueRate": 0.68           // SECRET — strip on GET
    }
  ]
}
```

`trueRate` and `sampleSize` are the answer key. **Strip both from every `GET` response**;
they are returned only in the `POST` result.

`ResponseJson`: `{ "guesses": [0.55, 0.30, ...] }` — five values in 0..1.

## Scoring

Per round, on percentage points:

```
error_i  = |guess_i - trueRate_i| * 100
round_i  = max(0, 100 - 2 * error_i)      // 0 at 50 points of error
```

Total `Score` = mean of the five, rounded. The 2× multiplier is deliberate: a flat
`100 - error` scores 60 for pure guessing, which feels unearned.

The summary line counts signed errors, not absolute ones — "you overestimated on 3 of 5" is
the interesting feedback, and systematic over-estimation of division is the specific bias
this game exists to surface.

## Share

Bucketed per round, so the grid conveys accuracy without leaking the true rates:

```
Crowd Call #88 — 82/100
🟩🟩🟨🟩🟥
I overestimated division on 3 of 5.
civersify.com/daily
```

🟩 within 10 points, 🟨 within 25, 🟥 beyond.

## Verification

- With an empty `QuizResponses` table, a puzzle still generates — entirely from the seeded
  poll bank.
- A question with 49 responses in-window is excluded; at 50 it becomes eligible.
- `GET` responses contain no `trueRate` or `sampleSize` field.
- The reveal names the crowd source, and a `national-poll` item shows its fielding date and
  source link.
- `QuizPollStats` and `/api/quiz/questions` report the same correct rate for the same
  question on the same day.
- Perfect guesses score 100; 50-point errors score 0, not negative.
