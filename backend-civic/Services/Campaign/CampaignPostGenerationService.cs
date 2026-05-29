using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Extends the bot-heartbeat pattern to candidate posts. Two triggers:
/// (1) a recently published Civic Briefing fans out to matching candidates;
/// (2) scheduled platform statements fill the gaps between news events.
/// Candidate selection and tone are decided deterministically (no LLM); only
/// the post body is generated, then length-enforced and auto-fragmented.
/// </summary>
public class CampaignPostGenerationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILlmClient _llm;
    private readonly IOptionsMonitor<CampaignOptions> _opts;
    private readonly ILogger<CampaignPostGenerationService> _log;

    public CampaignPostGenerationService(
        IServiceScopeFactory scopes,
        ILlmClient llm,
        IOptionsMonitor<CampaignOptions> opts,
        ILogger<CampaignPostGenerationService> log)
    {
        _scopes = scopes;
        _llm = llm;
        _opts = opts;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(35), stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_opts.CurrentValue.Enabled) await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "CampaignPostGenerationService: tick failed");
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, _opts.CurrentValue.GenerationIntervalMinutes));
            try { await Task.Delay(interval, stoppingToken); }
            catch (TaskCanceledException) { /* shutdown */ }
        }
    }

    /// <summary>One generation tick: fan out across newly-published briefings.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var opts = _opts.CurrentValue;
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var lookback = DateTime.UtcNow.AddHours(-opts.BriefingLookbackHours);
        var recentBriefings = await db.Briefings
            .Where(b => b.CreatedAt >= lookback)
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        var total = 0;
        foreach (var briefing in recentBriefings)
        {
            var alreadyCovered = await db.CampaignPosts.AnyAsync(p => p.TriggerBriefingSlug == briefing.Slug, ct);
            if (alreadyCovered) continue;
            total += await GenerateForBriefingInScopeAsync(scope, briefing, force: false, ct);
        }

        _log.LogInformation("CampaignPostGenerationService: generated {Count} posts", total);
        return total;
    }

    /// <summary>Generates posts for every candidate selected to respond to a briefing.</summary>
    public async Task<int> GenerateForBriefingAsync(Briefing briefing, bool force = false, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        return await GenerateForBriefingInScopeAsync(scope, briefing, force, ct);
    }

    /// <summary>Generates a single post for one candidate (used by the admin endpoint).</summary>
    public async Task<CampaignPost?> GenerateForCandidateAsync(
        Guid candidateId, Briefing? briefing, PostTrigger trigger, bool force = false, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var candidate = await LoadCandidateAsync(db, candidateId, ct);
        if (candidate is null) return null;
        return await GenerateForCandidateInScopeAsync(scope, candidate, briefing, trigger, force, ct);
    }

    private async Task<int> GenerateForBriefingInScopeAsync(
        IServiceScope scope, Briefing briefing, bool force, CancellationToken ct)
    {
        var selection = scope.ServiceProvider.GetRequiredService<ICandidateSelectionService>();
        var candidates = await selection.SelectForBriefingAsync(briefing, ct);

        var count = 0;
        foreach (var candidate in candidates)
        {
            var post = await GenerateForCandidateInScopeAsync(scope, candidate, briefing, PostTrigger.Briefing, force, ct);
            if (post is not null) count++;
        }
        return count;
    }

    private async Task<CampaignPost?> GenerateForCandidateInScopeAsync(
        IServiceScope scope, VirtualCandidate candidate, Briefing? briefing, PostTrigger trigger, bool force, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var selection = scope.ServiceProvider.GetRequiredService<ICandidateSelectionService>();

        var candidateTags = CandidateSelection.CandidateTags(candidate);
        var issueTags = ResolveIssueTags(candidate, candidateTags, briefing);
        var seed = HashCode.Combine(candidate.Id, briefing?.Slug ?? "platform");
        var (tone, intensity) = ToneResolver.Resolve(candidate, issueTags, seed);

        if (!force && !await selection.CanPostAsync(candidate.Id, intensity == 5, ct))
        {
            return null;
        }

        var planks = RankPlanks(candidate, issueTags);
        var sources = RankSources(candidate, issueTags);

        // Generate, then enforce the 160-char rule (re-prompt once, then truncate).
        var (sys, user) = CampaignPrompts.CampaignPost(candidate, tone, intensity, issueTags, planks, sources, briefing);
        var dto = await _llm.GenerateStructuredAsync<GeneratedCampaignPostDto>(sys, user, LlmModelTier.Sonnet, maxTokens: 400, ct: ct);
        var raw = dto.Body;
        var cited = dto.CitedReference;

        if (CampaignContentSanitizer.ExceedsLimit(raw))
        {
            var (sys2, user2) = CampaignPrompts.CampaignPost(candidate, tone, intensity, issueTags, planks, sources, briefing, lengthReminder: true);
            var dto2 = await _llm.GenerateStructuredAsync<GeneratedCampaignPostDto>(sys2, user2, LlmModelTier.Sonnet, maxTokens: 400, ct: ct);
            raw = dto2.Body;
            cited = dto2.CitedReference ?? cited;
        }

        var (body, _) = CampaignContentSanitizer.Clean(raw);
        if (string.IsNullOrWhiteSpace(body)) return null;

        cited ??= planks.FirstOrDefault()?.Title ?? sources.FirstOrDefault()?.Title;

        var post = new CampaignPost
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            Body = body,
            Tone = tone,
            Intensity = intensity,
            IssueTags = issueTags.ToArray(),
            Trigger = trigger,
            TriggerBriefingSlug = briefing?.Slug,
            CitedReference = cited,
            CreatedAt = DateTime.UtcNow,
        };

        var fragments = FragmentSplitter.Split(body);
        foreach (var f in fragments) f.PostId = post.Id;
        post.Fragments = fragments;

        db.CampaignPosts.Add(post);
        await db.SaveChangesAsync(ct);

        _log.LogInformation("Generated post {PostId} for candidate {Slug} ({Tone}/{Intensity})",
            post.Id, candidate.Slug, tone, intensity);
        return post;
    }

    private static List<string> ResolveIssueTags(
        VirtualCandidate candidate, IReadOnlyCollection<string> candidateTags, Briefing? briefing)
    {
        if (briefing is not null)
        {
            var briefingTags = briefing.Tags.Concat(briefing.ValuesInConflict).ToList();
            var matched = briefingTags
                .Where(b => candidateTags.Any(c =>
                    string.Equals(c, b, StringComparison.OrdinalIgnoreCase)
                    || c.Contains(b, StringComparison.OrdinalIgnoreCase)
                    || b.Contains(c, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();
            if (matched.Count > 0) return matched;
            if (briefingTags.Count > 0) return briefingTags.Take(2).ToList();
        }

        return candidate.PlatformPlanks.FirstOrDefault()?.IssueTags.Take(3).ToList() ?? new List<string>();
    }

    private static List<PlatformPlank> RankPlanks(VirtualCandidate candidate, IReadOnlyList<string> issueTags) =>
        candidate.PlatformPlanks
            .OrderByDescending(p => CandidateSelection.IssueMatchScore(p.IssueTags, issueTags))
            .ToList();

    private static List<CandidateSource> RankSources(VirtualCandidate candidate, IReadOnlyList<string> issueTags) =>
        candidate.Sources
            .OrderBy(s => s.Priority)
            .ThenByDescending(s => CandidateSelection.IssueMatchScore(s.IssueTags, issueTags))
            .ToList();

    private static Task<VirtualCandidate?> LoadCandidateAsync(CivicDbContext db, Guid id, CancellationToken ct) =>
        db.VirtualCandidates
            .Include(c => c.PlatformPlanks)
            .Include(c => c.Sources)
            .Include(c => c.IssueTones)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
}
