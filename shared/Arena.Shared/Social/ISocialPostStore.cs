using Microsoft.EntityFrameworkCore;

namespace Arena.Shared.Social;

/// <summary>
/// The publisher's ONLY persistence surface (SocialPublisher_Spec §4.4 isolation). Decouples the
/// shared publish engine + review/health services from any specific app DbContext, so the same core
/// runs in both the debate (Arena) and civic apps over their own SocialPosts table.
/// </summary>
public interface ISocialPostStore
{
    void Add(SocialPost post);
    void Save();
    Task SaveAsync(CancellationToken ct);

    /// <summary>Publishable rows: Pending or Approved with no PlatformPostId yet. Materialized; the
    /// caller applies the in-memory retry-gate + score ordering (provider-agnostic, §4.4).</summary>
    IReadOnlyList<SocialPost> GetPublishable();

    /// <summary>Count of posts already Published today on a platform (proactive daily-cap check).</summary>
    int CountPublishedToday(string platform, DateTimeOffset now);

    Task<IReadOnlyList<SocialPost>> ListByStatusAsync(SocialPostStatus status, CancellationToken ct);
    Task<SocialPost?> FindAsync(Guid id, CancellationToken ct);

    /// <summary>Published or Failed rows, for the health report's per-platform daily counts.</summary>
    Task<IReadOnlyList<SocialPost>> ListPublishedOrFailedAsync(CancellationToken ct);
}

/// <summary>
/// Generic EF Core store over any DbContext that maps <see cref="SocialPost"/>. Register per app:
/// <c>AddScoped&lt;ISocialPostStore&gt;(sp =&gt; new EfSocialPostStore(sp.GetRequiredService&lt;MyDbContext&gt;()))</c>.
/// </summary>
public sealed class EfSocialPostStore : ISocialPostStore
{
    private readonly DbContext _db;
    public EfSocialPostStore(DbContext db) => _db = db;

    private DbSet<SocialPost> Set => _db.Set<SocialPost>();

    public void Add(SocialPost post) => Set.Add(post);
    public void Save() => _db.SaveChanges();
    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public IReadOnlyList<SocialPost> GetPublishable() =>
        Set.Where(p => (p.Status == SocialPostStatus.Pending || p.Status == SocialPostStatus.Approved)
                       && p.PlatformPostId == null)
           .ToList();

    public int CountPublishedToday(string platform, DateTimeOffset now)
    {
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return Set.Where(p => p.Platform == platform && p.Status == SocialPostStatus.Published)
                  .AsEnumerable()
                  .Count(p => p.PublishedAt != null && p.PublishedAt >= dayStart);
    }

    public async Task<IReadOnlyList<SocialPost>> ListByStatusAsync(SocialPostStatus status, CancellationToken ct) =>
        await Set.Where(p => p.Status == status).OrderByDescending(p => p.PostScore).ToListAsync(ct);

    public Task<SocialPost?> FindAsync(Guid id, CancellationToken ct) =>
        Set.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<SocialPost>> ListPublishedOrFailedAsync(CancellationToken ct) =>
        await Set.Where(p => p.Status == SocialPostStatus.Published || p.Status == SocialPostStatus.Failed)
                 .ToListAsync(ct);
}
