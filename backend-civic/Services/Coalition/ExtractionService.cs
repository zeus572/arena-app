using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Coalition;

/// <summary>
/// Phase 0.3 — THE extraction function (the riskiest, highest-value component,
/// A7). Maps a free-form version's text to resolved positions on the known
/// sub-questions and surfaces any new sub-question it introduces (A4). Cached by
/// normalized text hash + known-sub-question signature (A5).
/// </summary>
public interface IExtractionService
{
    /// <summary>
    /// extract(versionText, knownSubQuestions) -> { positions, newSubQuestions }.
    /// Cached: repeated calls with the same text + known set do not re-hit the LLM.
    /// </summary>
    Task<ExtractionResult> ExtractAsync(
        string versionText,
        IReadOnlyList<SubQuestion> knownSubQuestions,
        CancellationToken ct = default);
}

public class ExtractionService : IExtractionService
{
    private readonly CivicDbContext _db;
    private readonly ILlmClient _llm;
    private readonly ILogger<ExtractionService> _log;
    private readonly ILlmAccessPolicy? _policy;

    // A7: extraction fidelity is the load-bearing risk, so the default tier is
    // Sonnet (quality over cost). The cache bounds the cost. Swap to Haiku and
    // re-run the fidelity harness if cost dominates.
    private const LlmModelTier ExtractionTier = LlmModelTier.Sonnet;

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ExtractionService(CivicDbContext db, ILlmClient llm, ILogger<ExtractionService> log, ILlmAccessPolicy? policy = null)
    {
        _db = db;
        _llm = llm;
        _log = log;
        _policy = policy;
    }

    public async Task<ExtractionResult> ExtractAsync(
        string versionText,
        IReadOnlyList<SubQuestion> knownSubQuestions,
        CancellationToken ct = default)
    {
        var textHash = HashText(versionText);
        var signature = KnownSignature(knownSubQuestions);

        var cached = await _db.Set<ExtractionCacheEntry>()
            .FirstOrDefaultAsync(c => c.TextHash == textHash && c.KnownSignature == signature, ct);
        if (cached is not null)
        {
            _log.LogDebug("Extraction cache hit ({Hash})", textHash[..8]);
            return JsonSerializer.Deserialize<ExtractionResult>(cached.ResultJson, JsonOpts) ?? new();
        }

        _policy?.EnsureAllowed(); // gate: only premium users trigger the live extraction LLM (else caller falls back)
        var (sys, user) = CoalitionPrompts.Extract(versionText, knownSubQuestions);
        var dto = await _llm.GenerateStructuredAsync<ExtractionResultDto>(sys, user, ExtractionTier, ct: ct);

        var result = Map(dto);

        _db.Set<ExtractionCacheEntry>().Add(new ExtractionCacheEntry
        {
            Id = Guid.NewGuid(),
            TextHash = textHash,
            KnownSignature = signature,
            ResultJson = JsonSerializer.Serialize(result),
            Model = ExtractionTier.ToString(),
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return result;
    }

    private static ExtractionResult Map(ExtractionResultDto dto) => new()
    {
        Positions = dto.Positions ?? new(),
        NewSubQuestions = (dto.NewSubQuestions ?? new()).Select(n => new ExtractedNewSubQuestion
        {
            Key = n.Key,
            Prompt = n.Prompt,
            Tradeoff = n.Tradeoff,
            PositionOptions = n.PositionOptions ?? Array.Empty<string>(),
            PositionInThisText = n.PositionInThisText,
        }).ToList(),
    };

    /// <summary>
    /// SHA-256 (hex) of normalized text: trimmed + internal whitespace collapsed.
    /// Case is preserved (it can carry meaning). This is the cache key + version
    /// dedup key.
    /// </summary>
    public static string HashText(string text)
    {
        var normalized = Whitespace.Replace(text ?? "", " ").Trim();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Stable signature of the known sub-question set (sorted keys, hashed). Two
    /// calls with the same known keys share a cache slot regardless of order.
    /// </summary>
    public static string KnownSignature(IReadOnlyList<SubQuestion> known)
    {
        var keys = known.Select(k => k.Key).OrderBy(k => k, StringComparer.Ordinal);
        var joined = string.Join(",", keys);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
