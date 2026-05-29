using Civic.API.Data;
using Civic.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Campaign;

public record ReactionCounts(int Up, int Down);

public enum ReactionOutcome { PostNotFound, FragmentNotFound, Applied, Removed }

public record ReactionResult(ReactionOutcome Outcome, ReactionCounts Post, ReactionCounts? Fragment);

public interface ICampaignReactionService
{
    Task<ReactionResult> ReactAsync(
        string userId, Guid postId, Guid? fragmentId, ReactionType type, CancellationToken ct = default);

    Task<ReactionResult> RemoveAsync(
        string userId, Guid postId, Guid? fragmentId, CancellationToken ct = default);
}

/// <summary>
/// Idempotent reaction writes per (userId, postId, fragmentId?). Re-reacting
/// with the same type is a no-op; switching type flips it. Aggregate counters
/// are recomputed from the reaction rows inside the same SaveChanges so they
/// can't drift, and the whole operation is wrapped in a transaction.
/// </summary>
public class CampaignReactionService : ICampaignReactionService
{
    private readonly CivicDbContext _db;

    public CampaignReactionService(CivicDbContext db) => _db = db;

    public async Task<ReactionResult> ReactAsync(
        string userId, Guid postId, Guid? fragmentId, ReactionType type, CancellationToken ct = default)
    {
        var post = await _db.CampaignPosts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return new ReactionResult(ReactionOutcome.PostNotFound, new(0, 0), null);

        PostFragment? fragment = null;
        if (fragmentId is not null)
        {
            fragment = await _db.PostFragments.FirstOrDefaultAsync(f => f.Id == fragmentId && f.PostId == postId, ct);
            if (fragment is null) return new ReactionResult(ReactionOutcome.FragmentNotFound, Counts(post), null);
        }

        var existing = await _db.PostReactions.FirstOrDefaultAsync(
            r => r.UserId == userId && r.PostId == postId && r.FragmentId == fragmentId, ct);

        if (existing is null)
        {
            _db.PostReactions.Add(new PostReaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PostId = postId,
                FragmentId = fragmentId,
                Type = type,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Type = type; // flip (idempotent if unchanged)
        }

        await RecomputeAndSaveAsync(post, fragment, ct);
        return new ReactionResult(ReactionOutcome.Applied, Counts(post), fragment is null ? null : Counts(fragment));
    }

    public async Task<ReactionResult> RemoveAsync(
        string userId, Guid postId, Guid? fragmentId, CancellationToken ct = default)
    {
        var post = await _db.CampaignPosts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return new ReactionResult(ReactionOutcome.PostNotFound, new(0, 0), null);

        PostFragment? fragment = null;
        if (fragmentId is not null)
        {
            fragment = await _db.PostFragments.FirstOrDefaultAsync(f => f.Id == fragmentId && f.PostId == postId, ct);
            if (fragment is null) return new ReactionResult(ReactionOutcome.FragmentNotFound, Counts(post), null);
        }

        var existing = await _db.PostReactions.FirstOrDefaultAsync(
            r => r.UserId == userId && r.PostId == postId && r.FragmentId == fragmentId, ct);
        if (existing is not null) _db.PostReactions.Remove(existing);

        await RecomputeAndSaveAsync(post, fragment, ct);
        return new ReactionResult(ReactionOutcome.Removed, Counts(post), fragment is null ? null : Counts(fragment));
    }

    private async Task RecomputeAndSaveAsync(CampaignPost post, PostFragment? fragment, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Flush the insert/delete/flip so the recount reflects it.
        await _db.SaveChangesAsync(ct);

        var postAgg = await _db.PostReactions
            .Where(r => r.PostId == post.Id && r.FragmentId == null)
            .GroupBy(r => r.Type)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        post.UpCount = postAgg.FirstOrDefault(a => a.Key == ReactionType.Up)?.Count ?? 0;
        post.DownCount = postAgg.FirstOrDefault(a => a.Key == ReactionType.Down)?.Count ?? 0;

        if (fragment is not null)
        {
            var fragAgg = await _db.PostReactions
                .Where(r => r.FragmentId == fragment.Id)
                .GroupBy(r => r.Type)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct);
            fragment.UpCount = fragAgg.FirstOrDefault(a => a.Key == ReactionType.Up)?.Count ?? 0;
            fragment.DownCount = fragAgg.FirstOrDefault(a => a.Key == ReactionType.Down)?.Count ?? 0;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static ReactionCounts Counts(CampaignPost p) => new(p.UpCount, p.DownCount);
    private static ReactionCounts Counts(PostFragment f) => new(f.UpCount, f.DownCount);
}
