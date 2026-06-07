using Civic.API.Data;
using Civic.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Coalition.Product;

/// <summary>
/// Seeds a couple of demo provisions with CONSTRUCTED agents (no LLM) so the
/// coalition loop is clickable end-to-end in the product:
///  1. an interactive provision (one agent corner + an open corner for the human),
///  2. an agent-only provision the user can drive to PASSED via repeated "agent step".
/// Idempotent by slug.
/// </summary>
public class CoalitionSeeder
{
    private readonly CivicDbContext _db;
    public CoalitionSeeder(CivicDbContext db) => _db = db;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedDataCenterFeeAsync(ct);
        await SeedAiHiringAsync(ct);
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedDataCenterFeeAsync(CancellationToken ct)
    {
        const string slug = "data-center-grid-fee-demo";
        if (await _db.Provisions.AnyAsync(p => p.Slug == slug, ct)) return;

        var id = Guid.NewGuid();
        _db.Provisions.Add(new Provision
        {
            Id = id, Slug = slug, Title = "Grid-cost fee on large data centers",
            NeutralText = "Large data centers should pay a fee reflecting the grid costs they impose, with carve-outs to be negotiated.",
            State = ProvisionState.Open,
            RelevantAxes = new[] { "market-vs-regulation", "local-vs-national" },
            Deadline = DateTime.UtcNow.AddDays(7),
            GenerationSource = CivicGenerationSource.Seed,
            SubQuestions =
            {
                new SubQuestion { Id = Guid.NewGuid(), Key = "scope", Prompt = "Which facilities are covered?", PositionOptions = new[] { "large-only", "all" }, OrderIndex = 0 },
                new SubQuestion { Id = Guid.NewGuid(), Key = "gf", Prompt = "Are existing facilities grandfathered?", PositionOptions = new[] { "exempt", "none" }, OrderIndex = 1 },
            },
            Versions =
            {
                new ProvisionVersion
                {
                    Id = Guid.NewGuid(), Label = "base",
                    Text = "Large data centers pay a grid-cost fee; no carve-out for existing facilities.",
                    TextHash = Guid.NewGuid().ToString("N"),
                    ExtractedPositions = new() { ["scope"] = "large-only", ["gf"] = "none" },
                    IsExtracted = true, ExtractionModel = "seed",
                },
            },
        });

        // One agent corner (market/right) that needs the grandfather carve-out. The human can
        // join as the other corner and bridge.
        _db.CoalitionParticipants.Add(new CoalitionParticipant
        {
            Id = Guid.NewGuid(), ProvisionId = id, UserId = "agent:marketcorner", SpectrumBucket = "right", IsAgent = true,
            RegionJson = CoalitionLoopService.RegionToJson(new Dictionary<string, string[]>
            {
                ["scope"] = new[] { "large-only", "all" }, ["gf"] = new[] { "exempt" },
            }),
            IntensitiesJson = CoalitionLoopService.IntensitiesToJson(new Dictionary<string, string> { ["gf"] = "High" }),
        });
    }

    private async Task SeedAiHiringAsync(CancellationToken ct)
    {
        const string slug = "ai-hiring-disclosure-demo";
        if (await _db.Provisions.AnyAsync(p => p.Slug == slug, ct)) return;

        var id = Guid.NewGuid();
        _db.Provisions.Add(new Provision
        {
            Id = id, Slug = slug, Title = "Disclosure + human review for AI hiring tools",
            NeutralText = "Employers using AI to screen candidates should disclose it and allow human review, with scope and depth to be negotiated.",
            State = ProvisionState.Open,
            RelevantAxes = new[] { "innovation-vs-precaution" },
            Deadline = DateTime.UtcNow.AddDays(7),
            GenerationSource = CivicGenerationSource.Seed,
            SubQuestions =
            {
                new SubQuestion { Id = Guid.NewGuid(), Key = "review", Prompt = "When is human review guaranteed?", PositionOptions = new[] { "on-request", "every-rejection" }, OrderIndex = 0 },
            },
            Versions =
            {
                new ProvisionVersion
                {
                    Id = Guid.NewGuid(), Label = "base",
                    Text = "AI hiring tools must disclose use; no human-review guarantee.",
                    TextHash = Guid.NewGuid().ToString("N"),
                    ExtractedPositions = new() { ["review"] = "none" },
                    IsExtracted = true, ExtractionModel = "seed",
                },
            },
        });

        // Three bridgeable agent corners across the spectrum: all accept "on-request" review.
        (string id, string bucket)[] corners = { ("agent:left", "left"), ("agent:center", "center"), ("agent:right", "right") };
        foreach (var (uid, bucket) in corners)
        {
            _db.CoalitionParticipants.Add(new CoalitionParticipant
            {
                Id = Guid.NewGuid(), ProvisionId = id, UserId = uid, SpectrumBucket = bucket, IsAgent = true,
                RegionJson = CoalitionLoopService.RegionToJson(new Dictionary<string, string[]>
                {
                    ["review"] = new[] { "on-request" },
                }),
                IntensitiesJson = CoalitionLoopService.IntensitiesToJson(new Dictionary<string, string> { ["review"] = "Medium" }),
            });
        }
    }
}
