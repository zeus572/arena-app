using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Models;

namespace Civic.API.Services;

public class SeedService : ISeedService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly CivicDbContext _db;
    private readonly ILogger<SeedService> _log;

    public SeedService(CivicDbContext db, ILogger<SeedService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedBriefingsAsync(ct);
        await SeedConceptsAsync(ct);
        await SeedThinkDeepersAsync(ct);
        await SeedQuestionsAsync(ct);
        await SeedElectionsAsync(ct);
        await SeedQuizQuestionsAsync(ct);
        await SeedBillTimelineAsync(ct);
        await SeedBillsAsync(ct);
        await SeedElectionCyclesAsync(ct);
        await SeedVirtualCandidatesAsync(ct);
    }

    private async Task SeedBillsAsync(CancellationToken ct)
    {
        var items = LoadJson<List<Bill>>("Seed.bills.json");
        if (items is null) return;

        var existing = await _db.Bills.Select(b => b.ExternalId).ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toAdd = items.Where(b => !existingSet.Contains(b.ExternalId)).ToList();
        foreach (var b in toAdd)
        {
            b.Id = Guid.NewGuid();
            b.IntroducedDate = DateTime.SpecifyKind(b.IntroducedDate, DateTimeKind.Utc);
            if (b.LatestActionDate is { } lad)
                b.LatestActionDate = DateTime.SpecifyKind(lad, DateTimeKind.Utc);
            b.IngestedAt = DateTime.UtcNow;
            b.SynthesisStatus = BillSynthesisStatus.Ingested;
            b.GenerationSource = CivicGenerationSource.Seed;
        }

        if (toAdd.Count > 0)
        {
            _db.Bills.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Seeded {Count} bills", toAdd.Count);
        }
    }

    private async Task SeedElectionCyclesAsync(CancellationToken ct)
    {
        var items = LoadJson<List<ElectionCycle>>("Seed.election-cycles.json");
        if (items is null) return;

        var existingSlugs = await _db.ElectionCycles.Select(c => c.Slug).ToListAsync(ct);
        var toAdd = items.Where(c => !existingSlugs.Contains(c.Slug)).ToList();
        foreach (var c in toAdd)
        {
            c.Id = Guid.NewGuid();
            c.ElectionDate = DateTime.SpecifyKind(c.ElectionDate, DateTimeKind.Utc);
            c.PrimarySeasonStart = DateTime.SpecifyKind(c.PrimarySeasonStart, DateTimeKind.Utc);
            c.GeneralSeasonStart = DateTime.SpecifyKind(c.GeneralSeasonStart, DateTimeKind.Utc);
            c.CreatedAt = DateTime.UtcNow;
        }

        if (toAdd.Count > 0)
        {
            _db.ElectionCycles.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Seeded {Count} election cycles", toAdd.Count);
        }
    }

    private async Task SeedVirtualCandidatesAsync(CancellationToken ct)
    {
        var items = LoadJson<List<VirtualCandidate>>("Seed.virtual-candidates.json");
        if (items is null) return;

        var existingSlugs = await _db.VirtualCandidates.Select(c => c.Slug).ToListAsync(ct);
        var toAdd = items.Where(c => !existingSlugs.Contains(c.Slug)).ToList();

        foreach (var c in toAdd)
        {
            c.Id = Guid.NewGuid();
            c.CreatedAt = DateTime.UtcNow;
            foreach (var s in c.AxisScores) { s.Id = Guid.NewGuid(); s.CandidateId = c.Id; }
            foreach (var t in c.IssueTones) { t.Id = Guid.NewGuid(); t.CandidateId = c.Id; }
            foreach (var p in c.PlatformPlanks) { p.Id = Guid.NewGuid(); p.CandidateId = c.Id; }
            foreach (var src in c.Sources) { src.Id = Guid.NewGuid(); src.CandidateId = c.Id; }
        }

        if (toAdd.Count > 0)
        {
            _db.VirtualCandidates.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Seeded {Count} virtual candidates", toAdd.Count);
        }
    }

    private async Task SeedBriefingsAsync(CancellationToken ct)
    {
        var items = LoadJson<List<Briefing>>("Seed.briefings.json");
        if (items is null) return;

        var existingSlugs = await _db.Briefings
            .Select(b => b.Slug)
            .ToListAsync(ct);

        var toAdd = items.Where(b => !existingSlugs.Contains(b.Slug)).ToList();
        foreach (var b in toAdd)
        {
            b.Id = Guid.NewGuid();
            b.CreatedAt = DateTime.UtcNow;
        }

        if (toAdd.Count > 0)
        {
            _db.Briefings.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Seeded {Count} briefings", toAdd.Count);
        }
    }

    private async Task SeedConceptsAsync(CancellationToken ct)
    {
        var items = LoadJson<List<Concept>>("Seed.concepts.json");
        if (items is null) return;

        var existingSlugs = await _db.Concepts
            .Select(c => c.Slug)
            .ToListAsync(ct);

        var toAdd = items.Where(c => !existingSlugs.Contains(c.Slug)).ToList();
        foreach (var c in toAdd) c.Id = Guid.NewGuid();

        if (toAdd.Count > 0)
        {
            _db.Concepts.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Seeded {Count} concepts", toAdd.Count);
        }
    }

    private async Task SeedThinkDeepersAsync(CancellationToken ct)
    {
        var items = LoadJson<List<ThinkDeeper>>("Seed.think-deepers.json");
        if (items is null) return;

        var existingSlugs = await _db.ThinkDeepers
            .Select(t => t.Slug)
            .ToListAsync(ct);

        var toAdd = items.Where(t => !existingSlugs.Contains(t.Slug)).ToList();
        foreach (var t in toAdd) t.Id = Guid.NewGuid();

        if (toAdd.Count > 0)
        {
            _db.ThinkDeepers.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Seeded {Count} think-deepers", toAdd.Count);
        }
    }

    private async Task SeedQuestionsAsync(CancellationToken ct)
    {
        var items = LoadJson<List<CivicQuestion>>("Seed.questions.json");
        if (items is null) return;

        var existingIds = await _db.CivicQuestions
            .Select(q => q.ExternalId)
            .ToListAsync(ct);

        var toAdd = items.Where(q => !existingIds.Contains(q.ExternalId)).ToList();
        foreach (var q in toAdd)
        {
            q.Id = Guid.NewGuid();
            q.CreatedAt = DateTime.UtcNow;
        }

        if (toAdd.Count > 0)
        {
            _db.CivicQuestions.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Seeded {Count} civic questions", toAdd.Count);
        }
    }

    private async Task SeedElectionsAsync(CancellationToken ct)
    {
        var items = LoadJson<List<Election>>("Seed.elections.json");
        if (items is null) return;

        var existingSlugs = await _db.Elections
            .Select(e => e.Slug)
            .ToListAsync(ct);

        var toAdd = items.Where(e => !existingSlugs.Contains(e.Slug)).ToList();
        foreach (var e in toAdd)
        {
            e.Id = Guid.NewGuid();
            e.ScheduledAt = DateTime.SpecifyKind(e.ScheduledAt, DateTimeKind.Utc);
            e.CreatedAt = DateTime.UtcNow;
        }

        if (toAdd.Count > 0)
        {
            _db.Elections.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Seeded {Count} elections", toAdd.Count);
        }
    }

    private async Task SeedQuizQuestionsAsync(CancellationToken ct)
    {
        var items = LoadJson<List<QuizQuestion>>("Seed.quiz-questions.json");
        if (items is null) return;

        var existing = await _db.QuizQuestions.Select(q => q.ExternalId).ToListAsync(ct);
        var toAdd = items.Where(q => !existing.Contains(q.ExternalId)).ToList();
        foreach (var q in toAdd) q.Id = Guid.NewGuid();

        if (toAdd.Count > 0)
        {
            _db.QuizQuestions.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Seeded {Count} quiz questions", toAdd.Count);
        }
    }

    private async Task SeedBillTimelineAsync(CancellationToken ct)
    {
        var items = LoadJson<List<BillTimelineStep>>("Seed.bill-timeline.json");
        if (items is null) return;

        var existing = await _db.BillTimelineSteps.Select(s => s.ExternalId).ToListAsync(ct);
        var toAdd = items.Where(s => !existing.Contains(s.ExternalId)).ToList();
        foreach (var s in toAdd) s.Id = Guid.NewGuid();

        if (toAdd.Count > 0)
        {
            _db.BillTimelineSteps.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Seeded {Count} bill timeline steps", toAdd.Count);
        }
    }

    public static T? LoadJson<T>(string resourceSuffix)
    {
        var asm = typeof(SeedService).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException(
                $"Embedded seed resource ending with '{resourceSuffix}' not found. " +
                "Ensure <EmbeddedResource Include=\"Seed\\**\\*.json\" /> is in Civic.API.csproj.");
        }

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Cannot open resource stream '{resourceName}'.");
        return JsonSerializer.Deserialize<T>(stream, JsonOpts);
    }
}
