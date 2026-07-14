using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Civic.API.Models;

namespace Civic.API.Services.Bills;

/// <summary>
/// Fetches recent congressional bills. Abstraction so the ingestion service can be
/// tested with a stub and so a future State/Local source can slot in behind it.
/// </summary>
public interface IBillSource
{
    /// <summary>
    /// Returns recently-updated bills mapped to <see cref="Bill"/> entities
    /// (status <see cref="BillSynthesisStatus.Ingested"/>). Returns an empty list
    /// on any failure or when no key is configured — never throws.
    /// </summary>
    Task<IReadOnlyList<Bill>> FetchRecentAsync(int congress, int limit, CancellationToken ct = default);
}

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper over the Congress.gov v3 REST API
/// (https://api.congress.gov/). Fails soft: logs and returns an empty list on
/// missing key / non-2xx / parse errors, mirroring the news sources' posture.
/// </summary>
public class CongressGovClient : IBillSource
{
    private readonly HttpClient _http;
    private readonly ILogger<CongressGovClient> _log;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public CongressGovClient(HttpClient http, string apiKey, ILogger<CongressGovClient> log)
    {
        _http = http;
        _log = log;
        _apiKey = apiKey;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://api.congress.gov/");
        }
    }

    public async Task<IReadOnlyList<Bill>> FetchRecentAsync(int congress, int limit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _log.LogInformation("CongressGovClient: no API key configured; skipping live ingestion");
            return Array.Empty<Bill>();
        }

        try
        {
            var listUrl = $"v3/bill/{congress}?format=json&limit={limit}&sort=updateDate+desc&api_key={_apiKey}";
            var list = await _http.GetFromJsonAsync<BillListResponse>(listUrl, JsonOpts, ct);
            if (list?.Bills is null || list.Bills.Count == 0)
            {
                return Array.Empty<Bill>();
            }

            var results = new List<Bill>();
            foreach (var wire in list.Bills)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(wire.Type) || wire.Number is null) continue;

                var detail = await TryFetchDetailAsync(congress, wire.Type, wire.Number.Value, ct);
                results.Add(MapBill(congress, wire, detail));
            }

            _log.LogInformation("CongressGovClient: fetched {Count} bills for congress {Congress}", results.Count, congress);
            return results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "CongressGovClient: fetch failed; returning no bills");
            return Array.Empty<Bill>();
        }
    }

    private async Task<BillDetail?> TryFetchDetailAsync(int congress, string type, int number, CancellationToken ct)
    {
        try
        {
            var url = $"v3/bill/{congress}/{type.ToLowerInvariant()}/{number}?format=json&api_key={_apiKey}";
            var resp = await _http.GetFromJsonAsync<BillDetailResponse>(url, JsonOpts, ct);
            return resp?.Bill;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "CongressGovClient: detail fetch failed for {Type}{Number}", type, number);
            return null;
        }
    }

    private static Bill MapBill(int congress, BillListItem wire, BillDetail? detail)
    {
        var type = wire.Type!.ToUpperInvariant();
        var number = wire.Number!.Value;
        var sponsor = detail?.Sponsors?.FirstOrDefault();

        return new Bill
        {
            Id = Guid.NewGuid(),
            ExternalId = $"{type.ToLowerInvariant()}-{number}-{congress}",
            Congress = congress,
            BillType = type,
            Number = number,
            Title = Truncate(wire.Title ?? detail?.Title ?? $"{type} {number}", 500),
            ShortTitle = null,
            Summary = Truncate(detail?.LatestSummaryText() ?? wire.LatestAction?.Text ?? "", 100_000),
            Sponsor = Truncate(sponsor?.FullName ?? "", 160),
            Party = sponsor?.Party,
            Status = MapStatus(wire.LatestAction?.Text),
            IntroducedDate = ParseDate(detail?.IntroducedDate) ?? ParseDate(wire.LatestAction?.ActionDate) ?? DateTime.UtcNow,
            LatestActionDate = ParseDate(wire.LatestAction?.ActionDate),
            FullTextUrl = wire.Url,
            SourceUrl = wire.Url,
            Jurisdiction = BillJurisdiction.Federal,
            SynthesisStatus = BillSynthesisStatus.Ingested,
            GenerationSource = "congress",
            IngestedAt = DateTime.UtcNow,
        };
    }

    private static BillStatus MapStatus(string? latestActionText)
    {
        if (string.IsNullOrWhiteSpace(latestActionText)) return BillStatus.Introduced;
        var t = latestActionText.ToLowerInvariant();
        if (t.Contains("became public law") || t.Contains("signed by president") || t.Contains("enacted"))
            return BillStatus.Enacted;
        if (t.Contains("passed congress") || t.Contains("presented to president"))
            return BillStatus.PassedBothChambers;
        if (t.Contains("passed") || t.Contains("agreed to in"))
            return BillStatus.PassedOneChamber;
        if (t.Contains("referred to") || t.Contains("committee"))
            return BillStatus.InCommittee;
        if (t.Contains("failed") || t.Contains("rejected"))
            return BillStatus.Failed;
        return BillStatus.Introduced;
    }

    private static DateTime? ParseDate(string? s) =>
        DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
            : null;

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max);

    // ---- Wire DTOs (subset of the Congress.gov v3 schema) ----

    private sealed class BillListResponse
    {
        public List<BillListItem>? Bills { get; set; }
    }

    private sealed class BillListItem
    {
        public string? Type { get; set; }
        public int? Number { get; set; }
        public string? Title { get; set; }
        public string? Url { get; set; }
        public LatestAction? LatestAction { get; set; }
    }

    private sealed class LatestAction
    {
        public string? ActionDate { get; set; }
        public string? Text { get; set; }
    }

    private sealed class BillDetailResponse
    {
        public BillDetail? Bill { get; set; }
    }

    private sealed class BillDetail
    {
        public string? Title { get; set; }
        public string? IntroducedDate { get; set; }
        public List<Sponsor>? Sponsors { get; set; }
        public Summaries? Summaries { get; set; }

        public string? LatestSummaryText()
        {
            var text = Summaries?.Items?.LastOrDefault()?.Text;
            return string.IsNullOrWhiteSpace(text) ? null : StripHtml(text);
        }

        private static string StripHtml(string s) =>
            System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", " ").Replace("&nbsp;", " ").Trim();
    }

    private sealed class Sponsor
    {
        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }
        public string? Party { get; set; }
    }

    private sealed class Summaries
    {
        [JsonPropertyName("summaries")]
        public List<SummaryItem>? Items { get; set; }
    }

    private sealed class SummaryItem
    {
        public string? Text { get; set; }
    }
}
