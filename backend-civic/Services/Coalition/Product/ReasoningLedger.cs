using Civic.API.Data;
using Civic.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Coalition.Product;

/// <summary>
/// The unified XP ledger. Writes a <see cref="CoalitionAct"/> row for any scored
/// activity — coalition acts, campaign news responses, post reactions, briefing
/// reads — and applies the reasoning-currency rules (within-day diminishing returns
/// plus the daily cap). Centralizing the write here lets non-coalition services
/// (campaigns, reactions) award XP without taking a dependency on the coalition loop,
/// so a player's reasoning XP reflects ALL of their engagement, not just coalition acts.
/// </summary>
public class ReasoningLedger
{
    private readonly CivicDbContext _db;

    public ReasoningLedger(CivicDbContext db) => _db = db;

    /// <summary>
    /// Record an act with already-computed governance/quality scores (no judging) and
    /// return the points awarded + currency. Lightweight activities (a reaction, a
    /// briefing read) call this with the defaults; coalition acts pass judge scores in.
    /// Reasoning-currency points are subject to the within-day diminishing curve and the
    /// daily cap, so awarding XP from many sources can't be farmed past the cap.
    /// </summary>
    public async Task<(int Points, string Currency)> RecordAsync(
        string userId, CoalitionActType type,
        string? payload = null, Guid? provisionId = null, Guid? versionId = null,
        int governance = 50, int quality = 50, int bonus = 0, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return (0, CoalitionPoints.Currency(type));

        var basePts = CoalitionPoints.BasePoints(type) + bonus;
        if (CoalitionPoints.QualityGated(type))
            basePts = Math.Max(1, (int)Math.Round(basePts * Math.Max(0.2, quality / 100.0)));

        var currency = CoalitionPoints.Currency(type);
        int points;
        if (currency == "reasoning")
        {
            var today = DateTime.UtcNow.Date;
            var todays = await _db.CoalitionActs
                .Where(a => a.UserId == userId && a.Currency == "reasoning" && a.CreatedAt >= today).ToListAsync(ct);
            points = CoalitionPoints.ApplyDiminishing(basePts, todays.Count, todays.Sum(a => a.Points));
        }
        else points = basePts;

        _db.CoalitionActs.Add(new CoalitionAct
        {
            Id = Guid.NewGuid(), UserId = userId, ProvisionId = provisionId, VersionId = versionId, Type = type,
            Payload = payload is null ? null : (payload.Length > 4000 ? payload[..4000] : payload),
            GovernanceScore = governance, QualityScore = quality, Points = points, Currency = currency,
        });
        await _db.SaveChangesAsync(ct);
        await LogActivityAsync(userId, ct);
        return (points, currency);
    }

    /// <summary>Mark the user active today (Layer 3.4 soft cadence). One row per (user, day).</summary>
    public async Task LogActivityAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (await _db.CoalitionActivityDays.AnyAsync(a => a.UserId == userId && a.Day == today, ct)) return;
        _db.CoalitionActivityDays.Add(new CoalitionActivityDay { Id = Guid.NewGuid(), UserId = userId, Day = today });
        await _db.SaveChangesAsync(ct);
    }
}
