using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services;
using Civic.API.Services.Bills;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/bills")]
public class BillsController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ICivicCatalog _catalog;

    public BillsController(CivicDbContext db, ICurrentUserService user, ICivicCatalog catalog)
    {
        _db = db;
        _user = user;
        _catalog = catalog;
    }

    /// <summary>
    /// Ranked list of synthesized bills, newest legislative action first.
    /// Optional <paramref name="jurisdiction"/> filter (defaults to Federal).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<BillSummaryDto>>> List(
        [FromQuery] string? jurisdiction = null,
        [FromQuery] int limit = 50)
    {
        var query = _db.Bills
            .Where(b => b.SynthesisStatus == BillSynthesisStatus.Synthesized);

        if (!string.IsNullOrWhiteSpace(jurisdiction)
            && Enum.TryParse<BillJurisdiction>(jurisdiction, ignoreCase: true, out var j))
        {
            query = query.Where(b => b.Jurisdiction == j);
        }

        var take = Math.Clamp(limit, 1, 200);
        var bills = await query
            .OrderByDescending(b => b.LatestActionDate ?? b.IntroducedDate)
            .Take(take)
            .Select(b => new
            {
                b.Id,
                b.ExternalId,
                b.Title,
                b.ShortTitle,
                b.Congress,
                b.BillType,
                b.Number,
                b.Sponsor,
                b.Party,
                b.Status,
                b.Jurisdiction,
                b.JurisdictionRegion,
                b.IntroducedDate,
                b.LatestActionDate,
                b.SynthesisSummary,
                b.Summary,
                AxisCount = b.AxisPositions.Count,
            })
            .ToListAsync();

        var dtos = bills.Select(b => new BillSummaryDto
        {
            Id = b.Id,
            ExternalId = b.ExternalId,
            Title = b.Title,
            ShortTitle = b.ShortTitle,
            Identifier = BillMappings.Identifier(b.BillType, b.Number, b.Congress),
            Sponsor = b.Sponsor,
            Party = b.Party,
            Status = b.Status.ToString(),
            Jurisdiction = b.Jurisdiction.ToString(),
            JurisdictionRegion = b.JurisdictionRegion,
            IntroducedDate = b.IntroducedDate,
            LatestActionDate = b.LatestActionDate,
            Teaser = BillMappings.Teaser(b.SynthesisSummary, b.Summary),
            AxisCount = b.AxisCount,
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Bill detail with its per-axis value positions and — when the caller has a
    /// scored compass — the user's own score and the alignment on each axis plus
    /// an overall alignment percentage.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BillDetailDto>> Get(Guid id)
    {
        var bill = await _db.Bills
            .Include(b => b.AxisPositions)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bill is null) return NotFound();

        var userId = _user.GetCurrentUserId();
        var profile = await _db.UserProfiles
            .Include(p => p.AxisScores)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        // A compass "counts" only when the user has actually answered questions
        // (ProfileVersion advances past the 0 used for locality-only upserts).
        var hasCompass = profile is { ProfileVersion: > 0 } && profile.AxisScores.Count > 0;
        var userByAxis = hasCompass
            ? profile!.AxisScores.ToDictionary(s => s.AxisKey, s => s.Score)
            : new Dictionary<string, double>();

        var positionsByAxis = bill.AxisPositions.ToDictionary(p => p.AxisKey);

        var axes = new List<BillAxisAlignmentDto>();
        var overallPairs = new List<(double, double, double)>();

        foreach (var pos in bill.AxisPositions)
        {
            var def = _catalog.AxisFor(pos.AxisKey);
            double? userScore = null;
            string? alignment = null;
            if (hasCompass && userByAxis.TryGetValue(pos.AxisKey, out var u))
            {
                userScore = u;
                alignment = BillAlignment.Classify(u, pos.Score);
                overallPairs.Add((u, pos.Score, pos.Confidence));
            }

            axes.Add(new BillAxisAlignmentDto
            {
                AxisKey = pos.AxisKey,
                AxisName = def?.Name ?? pos.AxisKey,
                LowLabel = def?.LowLabel ?? "",
                HighLabel = def?.HighLabel ?? "",
                Order = def?.Order ?? 999,
                BillScore = pos.Score,
                BillConfidence = pos.Confidence,
                Rationale = pos.Rationale,
                Evidence = pos.Evidence,
                UserScore = userScore,
                Alignment = alignment,
            });
        }

        var ordered = axes.OrderBy(a => a.Order).ToList();

        var dto = new BillDetailDto
        {
            Id = bill.Id,
            ExternalId = bill.ExternalId,
            Congress = bill.Congress,
            BillType = bill.BillType,
            Number = bill.Number,
            Identifier = BillMappings.Identifier(bill.BillType, bill.Number, bill.Congress),
            Title = bill.Title,
            ShortTitle = bill.ShortTitle,
            Summary = bill.Summary,
            SynthesisSummary = bill.SynthesisSummary,
            Sponsor = bill.Sponsor,
            Party = bill.Party,
            Status = bill.Status.ToString(),
            Jurisdiction = bill.Jurisdiction.ToString(),
            JurisdictionRegion = bill.JurisdictionRegion,
            IntroducedDate = bill.IntroducedDate,
            LatestActionDate = bill.LatestActionDate,
            FullTextUrl = bill.FullTextUrl,
            SourceUrl = bill.SourceUrl,
            HasUserCompass = hasCompass,
            OverallAlignmentPercent = hasCompass ? BillAlignment.OverallPercent(overallPairs) : null,
            Axes = ordered,
        };

        return Ok(dto);
    }
}
