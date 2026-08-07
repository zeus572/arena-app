using System.Text;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Civic.API.Services.Rooms;

/// <summary>One source that matched a theme room's terms, and which terms it hit.</summary>
public record CandidateMatch(
    ObjectType SourceType,
    Guid SourceId,
    string Slug,
    string Title,
    DateTime OccurredAt,
    IReadOnlyList<string> MatchedTerms);

/// <summary>
/// Finds sources that might belong in a theme room. Deterministic, no LLM, zero cost.
///
/// This is deliberately the dumb half of the pipeline. Whole-word matching against
/// <see cref="ThemeRoom.MatchTerms"/> is cheap enough to run over everything, produces the
/// same answer twice, and is unit-testable without a network — so the expensive half only
/// ever sees items that already cleared a mechanical bar.
///
/// It reads <b>Briefings and Bills only</b>, never <c>NewsItem</c>. Civic stores a headline
/// and an RSS summary for news, so a "claim" extracted from one would be a restatement of
/// its headline and could not satisfy PRD 04's exact-supporting-passage requirement.
/// Briefings and bills carry real prose. That constraint is enforced here, at the point
/// where the corpus is chosen, rather than left to the prompt to respect.
/// </summary>
public class RoomCandidateService
{
    private readonly CivicDbContext _db;
    private readonly IOptionsMonitor<RoomDraftOptions> _opts;
    private readonly ILogger<RoomCandidateService> _log;

    public RoomCandidateService(
        CivicDbContext db,
        IOptionsMonitor<RoomDraftOptions> opts,
        ILogger<RoomCandidateService> log)
    {
        _db = db;
        _opts = opts;
        _log = log;
    }

    /// <summary>
    /// Whole-word, case-insensitive containment.
    ///
    /// Substring matching is what makes a term list quietly useless: "aid" inside "said",
    /// "cut" inside "executive". Multi-word terms are matched as phrases with word
    /// boundaries at each end.
    /// </summary>
    public static bool ContainsTerm(string haystack, string term)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(term)) return false;

        var i = 0;
        while (i <= haystack.Length - term.Length)
        {
            var found = haystack.IndexOf(term, i, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return false;

            var beforeOk = found == 0 || !char.IsLetterOrDigit(haystack[found - 1]);
            var after = found + term.Length;
            var afterOk = after >= haystack.Length || !char.IsLetterOrDigit(haystack[after]);

            if (beforeOk && afterOk) return true;
            i = found + 1;
        }

        return false;
    }

    /// <summary>Which of a room's terms this text hits. Order follows the room's list.</summary>
    public static List<string> MatchedTerms(string text, IEnumerable<string> terms) =>
        terms.Where(t => ContainsTerm(text, t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Drives one pass over every live theme room. Public for deterministic tests.</summary>
    public async Task<int> RunPassAsync(CancellationToken ct = default)
    {
        var opts = _opts.CurrentValue;
        if (!opts.CandidatesEnabled) return 0;

        var rooms = await _db.ThemeRooms
            .Where(r => r.Status == RoomStatus.Published || r.Status == RoomStatus.Monitoring)
            .ToListAsync(ct);

        var created = 0;
        foreach (var room in rooms)
        {
            created += await ScanRoomAsync(room, opts, ct);
        }

        return created;
    }

    /// <summary>Scan one room. Returns how many new candidate story rooms were created.</summary>
    public async Task<int> ScanRoomAsync(
        ThemeRoom room, RoomDraftOptions opts, CancellationToken ct = default)
    {
        if (room.MatchTerms.Length == 0) return 0;

        var since = DateTime.UtcNow.AddDays(-Math.Max(1, room.DevelopmentWindowDays));

        var briefings = await _db.Briefings.AsNoTracking()
            .Where(b => b.CreatedAt >= since)
            .ToListAsync(ct);

        var bills = await _db.Bills.AsNoTracking()
            .Where(b => b.IntroducedDate >= since)
            .ToListAsync(ct);

        var matches = new List<CandidateMatch>();

        foreach (var b in briefings)
        {
            var terms = MatchedTerms(BriefingHaystack(b), room.MatchTerms);
            if (terms.Count < opts.MinTermHits) continue;
            matches.Add(new CandidateMatch(
                ObjectType.Briefing, b.Id, b.Slug, b.Headline, b.CreatedAt, terms));
        }

        foreach (var b in bills)
        {
            var terms = MatchedTerms(BillHaystack(b), room.MatchTerms);
            if (terms.Count < opts.MinTermHits) continue;
            matches.Add(new CandidateMatch(
                ObjectType.Bill, b.Id, b.ExternalId, b.ShortTitle ?? b.Title, b.IntroducedDate, terms));
        }

        // THE HONEST DENOMINATOR. Design 1g prints "we logged N articles and judged M of
        // them to have changed something", so N has to be what was actually looked at over
        // the window the page advertises.
        //
        // Recomputed rather than incremented: the window slides, and a counter that only
        // ever goes up would keep claiming credit for articles that fell out of it months
        // ago. A number that drifts upward while the window stays fixed is a lie that grows.
        //
        // Except for seeded rooms. A hand-authored room's developments were written against
        // a corpus this instance may not hold — the pilot's were drawn from production —
        // so recomputing here would overwrite a true number with one measured against the
        // wrong library. Whoever authored the content owns the denominator that goes with it.
        if (room.GenerationSource != CivicGenerationSource.Seed)
        {
            room.ArticlesConsideredCount = briefings.Count + bills.Count;
        }

        var created = 0;
        foreach (var m in matches.OrderByDescending(m => m.OccurredAt).Take(opts.MaxCandidatesPerRoom))
        {
            if (await AlreadyCapturedAsync(m, ct)) continue;

            var slug = CandidateSlug(room.Slug, m.Slug);
            if (await _db.Rooms.AnyAsync(r => r.Slug == slug, ct)) continue;

            var story = new StoryRoom
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Title = Truncate(m.Title, 300),
                Dek = "",
                // Candidate, not Draft: nothing has written this room yet. The draft pass
                // is what moves it on, and neither pass ever reaches Published.
                Status = RoomStatus.Candidate,
                StoryType = RoomTopicCategory.Legislative,
                EventTime = m.OccurredAt,
                Revision = 1,
                GenerationSource = CivicGenerationSource.Model,
                SourceBriefingId = m.SourceType == ObjectType.Briefing ? m.SourceId : null,
                SourceBillId = m.SourceType == ObjectType.Bill ? m.SourceId : null,
            };

            _db.Rooms.Add(story);
            await _db.SaveChangesAsync(ct);
            created++;

            _log.LogInformation(
                "RoomCandidateService: {Room} matched {Source} on [{Terms}]",
                room.Slug, slug, string.Join(", ", m.MatchedTerms));
        }

        await _db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>True when this source already has a room, at any status.</summary>
    private Task<bool> AlreadyCapturedAsync(CandidateMatch m, CancellationToken ct) =>
        m.SourceType == ObjectType.Briefing
            ? _db.StoryRooms.AnyAsync(r => r.SourceBriefingId == m.SourceId, ct)
            : _db.StoryRooms.AnyAsync(r => r.SourceBillId == m.SourceId, ct);

    /// <summary>
    /// Prefixed with the theme room so two rooms matching the same briefing do not collide
    /// on the unique slug index, and so a candidate is identifiable at a glance.
    /// </summary>
    public static string CandidateSlug(string roomSlug, string sourceSlug)
    {
        var s = $"{roomSlug}-{sourceSlug}";
        return s.Length <= 160 ? s : s[..160];
    }

    /// <summary>Everything on a briefing worth matching against. Real prose, by design.</summary>
    private static string BriefingHaystack(Briefing b)
    {
        var sb = new StringBuilder();
        sb.Append(b.Headline).Append(' ')
          .Append(b.KeyConcept).Append(' ')
          .Append(b.Summary30).Append(' ')
          .Append(b.WhatChanged).Append(' ')
          .Append(b.WhoActed).Append(' ')
          .Append(string.Join(' ', b.Tags));
        return sb.ToString();
    }

    private static string BillHaystack(Bill b) =>
        $"{b.ShortTitle} {b.Title} {b.Summary}";

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
}
