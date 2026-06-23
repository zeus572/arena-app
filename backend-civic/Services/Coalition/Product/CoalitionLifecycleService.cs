using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services.Coalition.Curriculum;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Coalition.Product;

/// <summary>
/// #4 Lifecycle automation. Pure orchestration over the loop service (no LLM of its
/// own): resolves provisions whose deadline has passed (→ DIED + payouts), tops up
/// the active pool by birthing new provisions from unused briefings (~the weekly
/// cadence), and executes promotion/relegation so players stay near their ability
/// edge. Driven by <see cref="CoalitionLifecycleHostedService"/>; methods are public
/// so they can be ticked deterministically in tests.
/// </summary>
public class CoalitionLifecycleService
{
    private readonly CivicDbContext _db;
    private readonly CoalitionLoopService _loop;
    private readonly ILogger<CoalitionLifecycleService>? _log;

    public const int TargetActiveProvisions = 7;

    public CoalitionLifecycleService(CivicDbContext db, CoalitionLoopService loop,
        ILogger<CoalitionLifecycleService>? log = null)
    {
        _db = db;
        _loop = loop;
        _log = log;
    }

    private static readonly ProvisionState[] Active =
        { ProvisionState.Open, ProvisionState.Contested, ProvisionState.NearCoalition };

    /// <summary>Resolve every active provision whose deadline has passed (→ DIED). Returns count resolved.</summary>
    public async Task<int> ResolveOverdueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var overdue = await _db.Provisions
            .Where(p => Active.Contains(p.State) && p.Deadline != null && p.Deadline <= now)
            .Select(p => p.Id).ToListAsync(ct);

        foreach (var id in overdue) await _loop.ResolveAsync(id, ct);
        return overdue.Count;
    }

    /// <summary>Birth provisions from unused briefings until the active pool hits the target. Returns count born.</summary>
    public async Task<int> TopUpAsync(int target = TargetActiveProvisions, CancellationToken ct = default)
    {
        var activeCount = await _db.Provisions.CountAsync(p => Active.Contains(p.State), ct);
        var born = 0;
        var needed = target - activeCount;
        while (activeCount + born < target)
        {
            var usedBriefingIds = await _db.Provisions
                .Where(p => p.SourceBriefingId != null).Select(p => p.SourceBriefingId!.Value).Distinct().ToListAsync(ct);
            var briefing = await _db.Briefings
                .Where(b => !usedBriefingIds.Contains(b.Id))
                .OrderBy(b => b.IssueOrder).FirstOrDefaultAsync(ct);
            if (briefing is null) break; // no unused briefings left

            // When filling an empty pool, stagger deadlines (1 day, 2 days, … target days) so the
            // rolling window is established immediately and provisions expire one per day.
            // When topping up a partially-populated pool, give each new provision the full lifetime.
            var daysOut = needed >= target ? born + 1 : target;
            try
            {
                await _loop.BirthFromBriefingAsync(briefing.Id, currentUserId: null, ct,
                    deadline: DateTime.UtcNow.AddDays(daysOut));
            }
            catch (LlmException ex)
            {
                // A live LLM call failed (e.g. Anthropic out of credits). Birthing now would only
                // persist generic "dead" provisions, so stop topping up this tick and retry next
                // tick — by when the key may have credit again.
                _log?.LogWarning(ex,
                    "Coalition top-up halted: LLM unavailable, birthed {Born} before bailing (won't synthesize dead provisions)", born);
                break;
            }
            born++;
        }
        return born;
    }

    /// <summary>Move players to the circle tier matching their skill (promotion/relegation). Returns count moved.</summary>
    public async Task<int> ApplyPromotionsAsync(CancellationToken ct = default)
    {
        var circles = await _db.CoalitionCircles.OrderBy(l => l.GapTier).ToListAsync(ct);
        if (circles.Count < 2) return 0;

        var members = await _db.CoalitionCircleMembers.ToListAsync(ct);
        var moved = 0;
        foreach (var m in members)
        {
            var circle = circles.FirstOrDefault(l => l.Id == m.CircleId);
            if (circle is null) continue;
            var idx = circles.FindIndex(l => l.Id == circle.Id);
            var skill = await _loop.GetUserSkillAsync(m.UserId, ct);

            var decision = PromotionService.Decide(skill, circle.GapTier);
            var targetIdx = decision switch
            {
                CircleMovement.Promote => Math.Min(idx + 1, circles.Count - 1),
                CircleMovement.Relegate => Math.Max(idx - 1, 0),
                _ => idx,
            };
            if (targetIdx != idx)
            {
                m.CircleId = circles[targetIdx].Id;
                moved++;
            }
        }
        if (moved > 0) await _db.SaveChangesAsync(ct);
        return moved;
    }

    /// <summary>
    /// Run agent ballast on active provisions (the prod replacement for the dev "Run agents"
    /// button): a few agent rounds per provision so coalitions can form/seed in thin rooms
    /// without a human clicking. Stops a provision early once it resolves. Returns rounds run.
    /// </summary>
    public async Task<int> StepAgentsAsync(int roundsPerProvision = 2, CancellationToken ct = default)
    {
        var ids = await _db.Provisions.Where(p => Active.Contains(p.State)).Select(p => p.Id).ToListAsync(ct);
        var rounds = 0;
        foreach (var id in ids)
        {
            for (var r = 0; r < roundsPerProvision; r++)
            {
                var detail = await _loop.AgentStepAsync(id, currentUserId: null, ct);
                rounds++;
                if (detail is null || detail.State is "Passed" or "Forked" or "Died") break;
            }
        }
        return rounds;
    }

    /// <summary>One lifecycle tick: resolve deadlines, top up, run agent ballast, re-balance circles.</summary>
    public async Task RunTickAsync(CancellationToken ct = default)
    {
        await ResolveOverdueAsync(ct);
        await TopUpAsync(TargetActiveProvisions, ct);
        await StepAgentsAsync(roundsPerProvision: 2, ct);
        await ApplyPromotionsAsync(ct);
    }
}
