using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services.Generation;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Coalition;

/// <summary>
/// Phase 0.2 — provision birth. Turns a <see cref="Briefing"/> into a born
/// <see cref="Provision"/>: a neutral-surface, real-tradeoff proposition, its
/// initial (Birth-origin) sub-questions, and its relevant Values-axis tags — via
/// a single extraction-tier LLM call at birth (A5).
///
/// This is the SYSTEM-extracted birth path (favored early for quality control,
/// doc 03 §1). The base version's text→positions extraction is Phase 0.3's job
/// and is intentionally not done here.
/// </summary>
public class ProvisionBirthService
{
    private readonly CivicDbContext _db;
    private readonly ILlmClient _llm;
    private readonly ILogger<ProvisionBirthService> _log;

    // ~1 week lifecycle target (doc 03).
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

    public ProvisionBirthService(
        CivicDbContext db,
        ILlmClient llm,
        ILogger<ProvisionBirthService> log)
    {
        _db = db;
        _llm = llm;
        _log = log;
    }

    /// <summary>
    /// Births a provision from a briefing, persists it (with its Birth
    /// sub-questions), and returns it. Throws <see cref="LlmException"/> if the
    /// extraction call fails (e.g. no API key configured).
    /// </summary>
    public async Task<Provision> BirthFromBriefingAsync(Briefing briefing, CancellationToken ct = default)
    {
        var (sys, user) = CoalitionPrompts.ProvisionBirth(briefing);
        // Birth framing quality matters (neutral surface / real tradeoff), so this
        // uses the Sonnet tier. It is a once-per-provision call, not a hot path.
        var dto = await _llm.GenerateStructuredAsync<GeneratedProvisionDto>(
            sys, user, LlmModelTier.Sonnet, ct: ct);

        var provision = await MapAndPersistAsync(dto, briefing, ct);
        _log.LogInformation(
            "Provision born from briefing {Slug}: {ProvisionSlug} ({SubQ} sub-questions, axes: {Axes})",
            briefing.Slug, provision.Slug, provision.SubQuestions.Count, string.Join(",", provision.RelevantAxes));
        return provision;
    }

    /// <summary>
    /// Maps a generated DTO onto a persisted provision. Exposed (internal) so the
    /// Phase 0.2 mechanics test can drive the mapping deterministically with a
    /// canned DTO without going through the LLM client.
    /// </summary>
    internal async Task<Provision> MapAndPersistAsync(GeneratedProvisionDto dto, Briefing briefing, CancellationToken ct = default)
    {
        var title = string.IsNullOrWhiteSpace(dto.Title) ? briefing.Headline : dto.Title.Trim();
        var slug = await UniqueSlugAsync(title, ct);

        var provision = new Provision
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Title = title,
            NeutralText = dto.NeutralText.Trim(),
            SourceBriefingId = briefing.Id,
            SourceBriefingSlug = briefing.Slug,
            // Born and immediately open for position-gathering. The formal
            // BIRTH->OPEN transition is owned by the Layer 2 state machine; we set
            // the natural post-birth state here so Layer 0 engagement can attach.
            State = ProvisionState.Open,
            RelevantAxes = (dto.RelevantAxes ?? Array.Empty<string>())
                .Select(a => a.Trim()).Where(a => a.Length > 0).ToArray(),
            Deadline = DateTime.UtcNow + DefaultLifetime,
            GenerationSource = briefing.GenerationSource,
            CreatedAt = DateTime.UtcNow,
        };

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 0;
        foreach (var sq in dto.SubQuestions ?? new())
        {
            if (string.IsNullOrWhiteSpace(sq.Prompt)) continue;
            var key = UniqueKey(sq.Key, sq.Prompt, seenKeys);
            provision.SubQuestions.Add(new SubQuestion
            {
                Id = Guid.NewGuid(),
                Key = key,
                Prompt = sq.Prompt.Trim(),
                TradeoffDescription = string.IsNullOrWhiteSpace(sq.Tradeoff) ? null : sq.Tradeoff.Trim(),
                PositionOptions = (sq.PositionOptions ?? Array.Empty<string>())
                    .Select(o => o.Trim()).Where(o => o.Length > 0).ToArray(),
                Origin = SubQuestionOrigin.Birth,
                OrderIndex = order++,
                CreatedAt = DateTime.UtcNow,
            });
        }

        _db.Provisions.Add(provision);
        await _db.SaveChangesAsync(ct);
        return provision;
    }

    private static string UniqueKey(string? proposed, string prompt, HashSet<string> seen)
    {
        var baseKey = Slugify.From(string.IsNullOrWhiteSpace(proposed) ? prompt : proposed, maxLength: 60);
        var candidate = baseKey;
        var i = 2;
        while (!seen.Add(candidate))
        {
            candidate = $"{baseKey}-{i++}";
        }
        return candidate;
    }

    private async Task<string> UniqueSlugAsync(string title, CancellationToken ct)
    {
        var baseSlug = Slugify.From(title, maxLength: 140);
        var candidate = baseSlug;
        var i = 2;
        while (await _db.Provisions.AnyAsync(p => p.Slug == candidate, ct))
        {
            candidate = $"{baseSlug}-{i++}";
        }
        return candidate;
    }
}
