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

    public const int TargetActiveProvisions = 4;

    public CoalitionLifecycleService(CivicDbContext db, CoalitionLoopService loop)
    {
        _db = db;
        _loop = loop;
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
        while (activeCount + born < target)
        {
            var usedBriefingIds = await _db.Provisions
                .Where(p => p.SourceBriefingId != null).Select(p => p.SourceBriefingId!.Value).Distinct().ToListAsync(ct);
            var briefing = await _db.Briefings
                .Where(b => !usedBriefingIds.Contains(b.Id))
                .OrderBy(b => b.IssueOrder).FirstOrDefaultAsync(ct);
            if (briefing is null) break; // no unused briefings left

            await _loop.BirthFromBriefingAsync(briefing.Id, currentUserId: null, ct);
            born++;
        }
        return born;
    }

    /// <summary>Move players to the league tier matching their skill (promotion/relegation). Returns count moved.</summary>
    public async Task<int> ApplyPromotionsAsync(CancellationToken ct = default)
    {
        var leagues = await _db.CoalitionLeagues.OrderBy(l => l.GapTier).ToListAsync(ct);
        if (leagues.Count < 2) return 0;

        var members = await _db.CoalitionLeagueMembers.ToListAsync(ct);
        var moved = 0;
        foreach (var m in members)
        {
            var league = leagues.FirstOrDefault(l => l.Id == m.LeagueId);
            if (league is null) continue;
            var idx = leagues.FindIndex(l => l.Id == league.Id);
            var skill = await _loop.GetUserSkillAsync(m.UserId, ct);

            var decision = PromotionService.Decide(skill, league.GapTier);
            var targetIdx = decision switch
            {
                LeagueMovement.Promote => Math.Min(idx + 1, leagues.Count - 1),
                LeagueMovement.Relegate => Math.Max(idx - 1, 0),
                _ => idx,
            };
            if (targetIdx != idx)
            {
                m.LeagueId = leagues[targetIdx].Id;
                moved++;
            }
        }
        if (moved > 0) await _db.SaveChangesAsync(ct);
        return moved;
    }

    /// <summary>One lifecycle tick: resolve deadlines, top up, then re-balance leagues.</summary>
    public async Task RunTickAsync(CancellationToken ct = default)
    {
        await ResolveOverdueAsync(ct);
        await TopUpAsync(TargetActiveProvisions, ct);
        await ApplyPromotionsAsync(ct);
    }
}
