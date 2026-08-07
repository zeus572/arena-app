using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Rooms;

/// <summary>
/// Turns drafted claims into real <see cref="Claim"/> rows with evidence edges.
///
/// The only interesting thing this does is refuse. A model asked for a verbatim supporting
/// passage will sometimes return a tidied one — same meaning, different words — and a
/// paraphrase presented as a quotation is the worst output this pipeline could produce,
/// because it looks exactly like the good case. So every passage is checked against the
/// source text, and a claim whose passage is not actually there loses its evidence edge and
/// is demoted rather than being published with a quotation nobody said.
///
/// <b>Sources are Briefings and Bills only.</b> Civic stores a headline and an RSS summary
/// for news items, so there is no body to extract from and no passage that could be
/// verified. <see cref="ExtractionSource"/> has no news arm, deliberately.
/// </summary>
public class ClaimExtractionService
{
    /// <summary>The prose a claim may be extracted from, and the row it came out of.</summary>
    public record ExtractionSource(
        ObjectType Type,
        Guid Id,
        string Title,
        string Url,
        string? Organization,
        DateTime? PublishedAt,
        string FullText);

    private readonly CivicDbContext _db;
    private readonly ObjectLinkService _links;
    private readonly ICivicCatalog _catalog;
    private readonly ILogger<ClaimExtractionService> _log;

    public ClaimExtractionService(
        CivicDbContext db,
        ObjectLinkService links,
        ICivicCatalog catalog,
        ILogger<ClaimExtractionService> log)
    {
        _db = db;
        _links = links;
        _catalog = catalog;
        _log = log;
    }

    public static ExtractionSource From(Briefing b) => new(
        ObjectType.Briefing, b.Id, b.Headline,
        $"/briefings/{b.Slug}", b.Institution, b.CreatedAt,
        string.Join("\n\n", new[]
        {
            b.WhoActed, b.WhatChanged, b.WhyItMatters, b.Disagreement,
            b.StrongestArgumentFor, b.StrongestArgumentAgainst,
            b.Summary3Min, b.Summary10Min,
        }.Where(s => !string.IsNullOrWhiteSpace(s))));

    public static ExtractionSource From(Bill b) => new(
        ObjectType.Bill, b.Id, b.ShortTitle ?? b.Title,
        b.FullTextUrl ?? $"/bills/{b.ExternalId}", "Congress.gov", b.IntroducedDate,
        string.Join("\n\n", new[] { b.Title, b.Summary }.Where(s => !string.IsNullOrWhiteSpace(s))));

    /// <summary>
    /// Whether a passage really appears in the source.
    ///
    /// Whitespace is normalised because line wrapping is not a quotation change, and curly
    /// quotes are folded because the model reliably straightens them. Nothing else is
    /// forgiven: a dropped clause or a swapped word means this returns false.
    /// </summary>
    public static bool PassageAppearsIn(string fullText, string passage)
    {
        if (string.IsNullOrWhiteSpace(passage)) return false;
        // Too short to be evidence of anything — "the bill" appears in every document.
        if (passage.Trim().Length < 25) return false;

        return Normalize(fullText).Contains(Normalize(passage), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string s) =>
        Regex.Replace(
            s.Replace('‘', '\'').Replace('’', '\'')
             .Replace('“', '"').Replace('”', '"')
             .Replace('–', '-').Replace('—', '-'),
            @"\s+", " ").Trim();

    /// <summary>
    /// Create claims for one drafted room and return them in draft order.
    ///
    /// Returns the created ids positionally so the caller can wire
    /// <see cref="DraftDimension.ClaimIndex"/> without a second lookup. A rejected claim
    /// yields a null in its slot rather than shifting everything after it.
    /// </summary>
    public async Task<List<Guid?>> CreateClaimsAsync(
        IReadOnlyList<DraftClaim> drafts,
        ExtractionSource source,
        CancellationToken ct = default)
    {
        var sourceRefId = await UpsertSourceRefAsync(source, ct);
        var result = new List<Guid?>();

        foreach (var d in drafts)
        {
            if (string.IsNullOrWhiteSpace(d.Text) || string.IsNullOrWhiteSpace(d.WhatWouldSettleIt))
            {
                // A claim with no text, or one nobody can say how to settle, is not a claim.
                result.Add(null);
                continue;
            }

            var verified = PassageAppearsIn(source.FullText, d.SupportingPassage);
            var status = ParseStatus(d.Status);

            if (!verified)
            {
                // The passage was not in the source. The claim may still be true, but we no
                // longer have grounds to say what supports it, so it cannot outrank
                // "reported, not established" and it gets no evidence edge.
                _log.LogWarning(
                    "ClaimExtractionService: unverified passage for claim '{Text}' from {Source}; demoting",
                    Truncate(d.Text, 80), source.Title);

                status = Demote(status);
            }

            var slug = await UniqueSlugAsync(Slugify(d.Text), ct);

            var claim = new Claim
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Text = Truncate(d.Text, 1000),
                Kind = ParseKind(d.Kind),
                Status = status,
                EvidenceSummary = Truncate(
                    verified
                        ? d.EvidenceSummary
                        : $"{d.EvidenceSummary} (The drafting pass could not verify a supporting "
                          + "passage in the source, so this has not been credited with the "
                          + "evidence it claimed.)",
                    2000),
                WhatWouldSettleIt = Truncate(d.WhatWouldSettleIt, 1000),
                GenerationSource = CivicGenerationSource.Model,
                Provenance = new List<FieldProvenance>
                {
                    new() { Field = nameof(Claim.Text), ProposedBy = ProvenanceOrigin.Model },
                    new() { Field = nameof(Claim.Status), ProposedBy = ProvenanceOrigin.Model },
                },
            };

            _db.Claims.Add(claim);
            await _db.SaveChangesAsync(ct);

            _db.ClaimStatusHistories.Add(new ClaimStatusHistory
            {
                Id = Guid.NewGuid(),
                ClaimId = claim.Id,
                FromStatus = null,
                ToStatus = claim.Status,
                ChangeKind = StatusChangeKind.InitialReview,
                Rationale = verified
                    ? "Drafted from a verified passage in the source document."
                    : "Drafted, but the supporting passage could not be found in the source.",
                ChangedAt = DateTime.UtcNow,
            });

            if (verified)
            {
                await _links.LinkAsync(
                    new ObjectRef(ObjectType.Claim, claim.Id),
                    LinkRelation.SupportedBy,
                    new ObjectRef(ObjectType.SourceRef, sourceRefId),
                    proposedBy: ProvenanceOrigin.Model, ct: ct);
            }

            await _db.SaveChangesAsync(ct);
            result.Add(claim.Id);
        }

        return result;
    }

    /// <summary>
    /// Nothing a model drafts may sit above "reported, not established".
    ///
    /// Confirmed means a primary document settles it. The pipeline holds a briefing, so the
    /// ceiling applies even when the passage checks out — and a claim whose passage did not
    /// check out drops further, to unsupported.
    /// </summary>
    public static ClaimStatus Cap(ClaimStatus status) => status switch
    {
        ClaimStatus.Confirmed => ClaimStatus.StronglySupported,
        _ => status,
    };

    private static ClaimStatus Demote(ClaimStatus status) => status switch
    {
        ClaimStatus.Confirmed or ClaimStatus.StronglySupported => ClaimStatus.PlausibleButUnresolved,
        ClaimStatus.PlausibleButUnresolved => ClaimStatus.Unsupported,
        // False and Outdated are findings about the claim, not credit given to it, so a
        // missing passage does not soften them.
        _ => status,
    };

    private async Task<Guid> UpsertSourceRefAsync(ExtractionSource source, CancellationToken ct)
    {
        var hash = Sha256(source.Url.Trim().ToLowerInvariant());
        var existing = await _db.SourceRefs.FirstOrDefaultAsync(s => s.UrlHash == hash, ct);
        if (existing is not null) return existing.Id;

        var row = new SourceRef
        {
            Id = Guid.NewGuid(),
            Url = source.Url,
            UrlHash = hash,
            Title = Truncate(source.Title, 500),
            Organization = source.Organization,
            SourceType = source.Type == ObjectType.Bill
                ? SourceType.PrimaryDocument
                : SourceType.Analysis,
            IsPrimary = source.Type == ObjectType.Bill,
            PublishedAt = source.PublishedAt,
            // We DO hold this one: it is our own prose or a bill summary, not a news body.
            FullTextAvailable = true,
        };

        _db.SourceRefs.Add(row);
        await _db.SaveChangesAsync(ct);
        return row.Id;
    }

    private async Task<string> UniqueSlugAsync(string baseSlug, CancellationToken ct)
    {
        var slug = baseSlug;
        var n = 2;
        while (await _db.Claims.AnyAsync(c => c.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{n++}";
            if (n > 50) return $"{baseSlug}-{Guid.NewGuid():N}"[..160];
        }
        return slug;
    }

    private static string Slugify(string text)
    {
        var s = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return s.Length <= 120 ? s : s[..120].TrimEnd('-');
    }

    private static ClaimStatus ParseStatus(string s) =>
        Cap(Enum.TryParse<ClaimStatus>(s, ignoreCase: true, out var v)
            ? v
            : ClaimStatus.PlausibleButUnresolved);

    private static ClaimKind ParseKind(string s) =>
        Enum.TryParse<ClaimKind>(s, ignoreCase: true, out var v) ? v : ClaimKind.Factual;

    private static string Sha256(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);

    /// <summary>Contested terms present in drafted text, for the reviewer's attention.</summary>
    public IReadOnlyList<string> ContestedTermsIn(string text) =>
        _catalog.ContestedTermsIn(text).Select(t => t.Term).ToList();
}
