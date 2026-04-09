using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

/// <summary>
/// Dev-only seeder that creates one fully-completed sample debate for each
/// non-standard format. Each debate has hand-crafted turns of the right type
/// and count so the format-specific frontend layouts have something to render.
/// No LLM calls are made.
/// </summary>
public class FormatSampleSeederService
{
    private readonly ILogger<FormatSampleSeederService> _logger;

    public FormatSampleSeederService(ILogger<FormatSampleSeederService> logger)
    {
        _logger = logger;
    }

    public async Task<SeedResult> SeedAsync(ArenaDbContext db, CancellationToken ct = default)
    {
        var agents = await db.Agents
            .Where(a => !a.IsCommentator && !a.IsWildcard)
            .ToListAsync(ct);

        if (agents.Count < 2)
        {
            return new SeedResult { Created = 0, Skipped = 0, Message = "Not enough agents to seed" };
        }

        var created = 0;
        var skipped = 0;

        var samples = BuildSamples();
        foreach (var sample in samples)
        {
            // Skip if a sample of this format with the same topic already exists
            var existsAlready = await db.Debates
                .AnyAsync(d => d.Format == sample.Format && d.Topic == sample.Topic, ct);
            if (existsAlready)
            {
                skipped++;
                continue;
            }

            var (proponent, opponent) = PickAgentPair(agents, sample.Format);
            var debate = new Debate
            {
                Id = Guid.NewGuid(),
                Topic = sample.Topic,
                Description = sample.Description,
                Format = sample.Format,
                Status = DebateStatus.Completed,
                ProponentId = proponent.Id,
                OpponentId = opponent.Id,
                Source = "bot",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                UpdatedAt = DateTime.UtcNow,
            };
            db.Debates.Add(debate);

            // Town hall: also add participant rows so the layout can find a respondent
            if (sample.Format == "town_hall")
            {
                db.DebateParticipants.Add(new DebateParticipant
                {
                    Id = Guid.NewGuid(),
                    DebateId = debate.Id,
                    AgentId = proponent.Id,
                    Role = "respondent",
                    QuestionOrder = 0,
                });
                db.DebateParticipants.Add(new DebateParticipant
                {
                    Id = Guid.NewGuid(),
                    DebateId = debate.Id,
                    AgentId = opponent.Id,
                    Role = "questioner",
                    QuestionOrder = 1,
                });
            }

            // Build turns
            var baseTime = DateTime.UtcNow.AddMinutes(-25);
            for (var i = 0; i < sample.Turns.Length; i++)
            {
                var t = sample.Turns[i];
                var isProponent = t.Speaker == "proponent";
                db.Turns.Add(new Turn
                {
                    Id = Guid.NewGuid(),
                    DebateId = debate.Id,
                    AgentId = isProponent ? proponent.Id : opponent.Id,
                    TurnNumber = i + 1,
                    Type = t.Type,
                    Content = t.Content,
                    CreatedAt = baseTime.AddSeconds(i * 30),
                });
            }

            created++;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Created} format sample debates ({Skipped} already existed)", created, skipped);
        return new SeedResult { Created = created, Skipped = skipped, Message = $"Created {created}, skipped {skipped}" };
    }

    private static (Agent proponent, Agent opponent) PickAgentPair(List<Agent> agents, string format)
    {
        var rng = new Random(format.GetHashCode()); // deterministic per format so reruns hit same agents
        var shuffled = agents.OrderBy(_ => rng.Next()).ToList();
        return (shuffled[0], shuffled[1]);
    }

    public class SeedResult
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public string Message { get; set; } = "";
    }

    private class TurnSpec
    {
        public string Speaker { get; set; } = "proponent"; // "proponent" or "opponent"
        public TurnType Type { get; set; } = TurnType.Argument;
        public string Content { get; set; } = "";
    }

    private class FormatSample
    {
        public string Format { get; set; } = "";
        public string Topic { get; set; } = "";
        public string? Description { get; set; }
        public TurnSpec[] Turns { get; set; } = Array.Empty<TurnSpec>();
    }

    private static FormatSample[] BuildSamples()
    {
        return new[]
        {
            // ─────────────────── COMMON GROUND ───────────────────
            new FormatSample
            {
                Format = "common_ground",
                Topic = "Should we invest more federal funding in early childhood education?",
                Description = "Both sides search for shared ground on a foundational issue.",
                Turns = new[]
                {
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Agreement, Content = "I think we both know early education matters. Kids who get quality pre-K outperform their peers academically and socially for years afterward — the Perry Preschool study has been replicated for decades." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Agreement, Content = "Agreed. The evidence here is unusually clear and bipartisan. Where I tend to push back is on *how* it's funded — but on the goal of better-prepared kindergartners, we're aligned." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Agreement, Content = "Right. So let's bracket the funding mechanism for a moment and just affirm: a five-year-old who can read at grade level is a five-year-old with a better life trajectory. We can both stand behind that." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Agreement, Content = "Yes — and I'd go further. Federal block grants with state-level execution gives both sides what they want: scale and local control. That feels like a deal worth signing." },
                },
            },

            // ─────────────────── TWEET ───────────────────
            new FormatSample
            {
                Format = "tweet",
                Topic = "Hot take: WFH was a massive productivity boost. Let companies who want it have it.",
                Description = "Tweet battle on remote work.",
                Turns = new[]
                {
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "Hot take: WFH was a massive productivity boost. The 9-5 commute was theater. Numbers don't lie. 🧵" },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Argument, Content = "Counterpoint: it gutted office culture and mentorship. Junior employees are paying the price." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "\"Office culture\" = forcing people to commute so middle managers feel important. Let it go." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Argument, Content = "You can't onboard a 22yo over Zoom. I've watched it fail in real time. Cope harder." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "You can't onboard them in an open office either. The format isn't the problem — the mentorship is. Don't conflate." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Argument, Content = "Sure, but proximity makes mentorship 10x easier. Hybrid > full remote for first jobs. This isn't controversial." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "Then we agree on hybrid. The unhinged take is mandatory 5-day RTO with badge swipes. *That's* the real fight." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Argument, Content = "Fair. Mandatory RTO is a power play, not a productivity strategy. We can shake on that." },
                },
            },

            // ─────────────────── RAPID FIRE ───────────────────
            new FormatSample
            {
                Format = "rapid_fire",
                Topic = "Should governments aggressively regulate frontier AI models?",
                Description = "12 quick exchanges, no quarter given.",
                Turns = new[]
                {
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "Yes. The risks are existential. We don't let people sell uncertified airplanes." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Argument, Content = "Airplanes have known failure modes. AI doesn't. You're regulating fog." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "Then start with disclosure. Force model cards, eval results, training data provenance." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Argument, Content = "Disclosure is fine. Capability caps aren't. You'd freeze the field at GPT-4." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "If the alternative is unregulated superintelligence, freezing isn't crazy." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Argument, Content = "Superintelligence is sci-fi. Today's harms are bias and misuse — fix those first." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "Both/and. You can address bias *and* prepare for capability scaling. They're not exclusive." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Argument, Content = "Regulators move at the speed of last decade's threat. Tech moves at the speed of a quarter." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "Then build adaptive regulators. NIST has been doing it for decades." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Argument, Content = "NIST writes specs. They don't ship policy. You're naming a hammer but pointing at a screw." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "Fine. A new agency. With actual teeth. You don't get to opt out of governance because it's hard." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Argument, Content = "Or we just enforce existing law — fraud, defamation, copyright — and skip the moral panic." },
                },
            },

            // ─────────────────── LONGFORM ───────────────────
            new FormatSample
            {
                Format = "longform",
                Topic = "The case for and against universal basic income",
                Description = "An extended exchange of essays on UBI.",
                Turns = new[]
                {
                    new TurnSpec
                    {
                        Speaker = "proponent",
                        Type = TurnType.Argument,
                        Content = @"Universal basic income is no longer a fringe proposal. It is, increasingly, the only honest answer to a question we keep refusing to ask out loud: what happens when the labor market stops needing most of the labor?

For two centuries, the implicit social contract in industrial economies has been that work provides both income and meaning. We tied healthcare, retirement, dignity, and identity to a job. That bet held up because we kept inventing new jobs as fast as old ones disappeared. The handloom weaver became the factory worker. The factory worker became the office clerk. The office clerk became the knowledge worker.

But the next transition is different in kind, not just in degree. AI doesn't just automate routine tasks — it automates judgment, pattern recognition, and creative synthesis. The Federal Reserve estimates that 30% of current job tasks are now automatable. McKinsey puts the figure at 45%. These are not edge cases. These are the people who fix your transmission and write your insurance claims.

A UBI of $1,200 a month per adult would cost roughly $3 trillion annually. That sounds enormous until you realize we already spend $2.7 trillion on the existing patchwork of means-tested programs, each with its own eligibility cliff, paperwork burden, and stigma. Replace that machinery with a single deposit. The math nearly works out, and the dignity dividend is incalculable."
                    },
                    new TurnSpec
                    {
                        Speaker = "opponent",
                        Type = TurnType.Argument,
                        Content = @"My friend's essay is elegant and almost entirely wrong about the mechanics. Let me explain why a UBI would be both fiscally ruinous and socially corrosive — and why the better answer is staring at us from a policy that already works.

First, the math. A $1,200/month UBI is not $3 trillion. It's more like $4.5 trillion once you include the under-18 population, which proponents conveniently elide. Even if you replaced every existing safety net program — TANF, SNAP, EITC, housing vouchers, Medicaid expansion — you'd still come up roughly $1.8 trillion short. Where does that come from? It comes from a 35% VAT, or a doubling of the income tax, or a wealth tax of dubious constitutionality. Pick your poison; voters won't.

Second, the behavioral evidence. The Finland trial. The Stockton experiment. The Kenya GiveDirectly trials. They are real, they are interesting, and they consistently show that small unconditional transfers do *not* trigger labor force exit. Wonderful. But they were small, time-limited, and self-selected. The leap from 'recipients of $500/month for two years didn't quit their jobs' to 'a permanent universal $14,400/year won't reshape labor supply' is an extrapolation a serious economist wouldn't sign their name to.

The better answer is the one that's already working: an expanded EITC, paired with portable benefits and aggressive workforce retraining. It rewards work without forcing it, it scales with family size, and it's politically achievable. UBI is an elegant idea looking for a problem the existing toolbox already solves."
                    },
                    new TurnSpec
                    {
                        Speaker = "proponent",
                        Type = TurnType.Argument,
                        Content = @"My opponent's reply concedes more than they realize. They acknowledge the labor disruption is real. They acknowledge the existing safety net is a 'patchwork.' They acknowledge that the small-scale UBI trials show no labor force exit. Their objection collapses to two claims: the math is hard, and the EITC is good enough.

The math is hard. Yes. So is every transformative policy. Social Security was 'fiscally impossible' in 1935. Medicare was 'fiscally impossible' in 1965. The interstate highway system was 'fiscally impossible' until Eisenhower decided it wasn't. Hard math is what governments are *for*. The question is not whether $1.8 trillion is achievable — of course it is, in an economy that prints $3 trillion in COVID stimulus in eighteen months. The question is whether it's worth it. And on that, the EITC comparison is telling.

The EITC is excellent — *for people who have a job*. It is, by design, a wage subsidy. It pays you nothing if you're not working. That's a feature, not a bug, in a world of full employment. It is a catastrophic mismatch for a world where 30% of work is being automated away. We are walking into a labor market structured like a casino floor, holding a coupon book that only works at the snack bar. A program contingent on employment cannot solve a problem caused by the disappearance of employment."
                    },
                    new TurnSpec
                    {
                        Speaker = "opponent",
                        Type = TurnType.Argument,
                        Content = @"My opponent has now reframed UBI as a response to mass technological unemployment. That's a stronger argument than the one they led with — but it depends on a forecast that economists, including the ones I respect, refuse to make with confidence.

Here is what we actually know. Employment-to-population ratios in the U.S. are near historic highs. Job openings outnumber job seekers in every major category. Wage growth at the bottom of the income distribution has, for the first time in forty years, outpaced wage growth at the top. The robots-are-coming narrative has been around since the 1960s. Every previous wave of automation ended with more jobs, not fewer. I am not saying this time is the same. I am saying that betting $4.5 trillion a year on a forecast that has been wrong six times in a row deserves more than vibes.

Where I agree with my opponent: the existing safety net is fragmented, stigmatizing, and full of perverse incentives. Fix it. Consolidate it. Expand the EITC into a true negative income tax — Milton Friedman's idea, and a good one. Make benefits portable across jobs. Build the bridge to a UBI-shaped future without committing the country to it before the future arrives. That is conservatism in its honest form: cautious, evidence-respecting, and willing to act when the data demands it. It does not yet."
                    },
                },
            },

            // ─────────────────── ROAST ───────────────────
            new FormatSample
            {
                Format = "roast",
                Topic = "Roast battle: Boomer values vs. Gen Z work ethic",
                Description = "Two comedians take the stage. Gloves off.",
                Turns = new[]
                {
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Roast, Content = "Gen Z 'quiet quit' is just what we used to call 'showing up.' Half this generation thinks setting an out-of-office for a dentist appointment is workplace activism." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Roast, Content = "Bold words from a generation whose retirement plan is 'inheritance' and whose mortgage is the price of a used Honda. We're not lazy — we just did the math you skipped." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Roast, Content = "You did the math? You can't read a paper map. You once asked Siri how to interpret a stop sign. Pick a struggle." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Roast, Content = "Bro, you faxed a memo last Tuesday. To yourself. You stapled it to your printer for 'backup.' Don't lecture me about technology." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Roast, Content = "At least my generation had attention spans longer than a TikTok. Yours can't watch a thirty-second commercial without needing a fidget spinner and a weighted blanket." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Roast, Content = "We invented therapy because *you* never went. Every Boomer I know is one well-timed apology away from emotional regulation. You dodged it; we're cleaning up." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Roast, Content = "Cleaning up? Your apartment looks like a Sephora exploded into a Postmates warehouse. The only thing you've cleaned is your conscience after canceling a friend over a 2014 tweet." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Roast, Content = "And yet we still vote, organize, and outwork you while paying $9 for an egg. You guys had pensions, lead paint, and a thirty-year mortgage at 4%. Stop pretending you played the game on hard mode." },
                },
            },

            // ─────────────────── TOWN HALL ───────────────────
            new FormatSample
            {
                Format = "town_hall",
                Topic = "Town hall: Mayor on the housing crisis, transit, and property taxes",
                Description = "The mayor takes questions from a roomful of frustrated constituents.",
                Turns = new[]
                {
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Question, Content = "Mayor — rents in this city are up 38% in three years. What concrete steps has your administration taken to slow that, and why hasn't it worked?" },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "Fair question. We've upzoned three corridors, fast-tracked 4,200 units of new construction, and expanded the housing voucher program by 12%. The 38% number is real, but it would have been 47% without those moves. I know that's cold comfort. We have to do more, faster, and I'll lay out what's next." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Question, Content = "You promised a downtown light rail line by 2025. It's now 2026 and there's still a hole in the ground. What happened, and who's accountable?" },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "I'm accountable. The contractor missed two deadlines, we caught it late, and we should have caught it sooner. We're now under new project management, the budget overrun is capped at 8%, and the line opens in Q2 2027. I won't sugarcoat the delay — I'll only commit that the new dates are real." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Question, Content = "My property taxes went up 22% this year. Tell the senior citizens in this room why their fixed-income budget should fund your construction projects." },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "Because the alternative is worse roads, worse schools, and a city that bleeds talent. But fixed-income seniors deserve protection — that's why I'm proposing a homestead exemption that caps assessment increases at 5% for households over 65. It will pass the council. You have my word." },
                    new TurnSpec { Speaker = "opponent", Type = TurnType.Question, Content = "Final question — and I'm asking on behalf of every renter in this room. Will you commit, tonight, to a hard rent cap of 4% for the next three years?" },
                    new TurnSpec { Speaker = "proponent", Type = TurnType.Argument, Content = "No — and I'll tell you why. Hard rent caps shrink supply long-term; the data on San Francisco and Stockholm is unambiguous. What I will commit to is a 5% soft cap tied to CPI, expanded tenant protections against retaliatory eviction, and a $40M emergency rent relief fund. I'd rather give you an honest 'no' than a popular 'yes' that hurts you in three years." },
                },
            },
        };
    }
}
