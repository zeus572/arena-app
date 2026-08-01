using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Arena.Shared.Llm;
using Arena.Shared.Reporting;

namespace Arena.API.Services.Reporting;

/// <summary>The rendered email, ready to hand to <c>IEmailSender</c>.</summary>
public record RenderedReport(string Subject, string Html, string Text);

/// <summary>
/// Turns the two apps' day slices into the operator's daily email: a "what happened today"
/// opener, then the numbers behind it, then where things stand overall.
///
/// The opener is written by Claude when available. That's the only non-deterministic part,
/// and it is strictly decorative — <see cref="Render"/> takes the narrative as an optional
/// string and produces a complete, accurate report without it. Any LLM failure (disabled,
/// keyless, rate-limited, non-JSON) falls back to a computed sentence, because a report that
/// doesn't arrive is worse than one without prose.
/// </summary>
public class DailyReportComposer
{
    private readonly ILlmClient _llm;
    private readonly DailyReportOptions _options;
    private readonly ILogger<DailyReportComposer> _logger;

    public DailyReportComposer(
        ILlmClient llm,
        IOptions<DailyReportOptions> options,
        ILogger<DailyReportComposer> logger)
    {
        _llm = llm;
        _options = options.Value;
        _logger = logger;
    }

    private sealed class Narrative
    {
        public string Headline { get; set; } = "";
        public string Summary { get; set; } = "";
    }

    public async Task<RenderedReport> ComposeAsync(
        DailyStatsDto arena,
        DailyStatsDto? civic,
        bool civicExpected,
        CancellationToken ct = default)
    {
        var narrative = await TryNarrativeAsync(arena, civic, ct);
        return Render(arena, civic, civicExpected, narrative);
    }

    private async Task<(string Headline, string Summary)?> TryNarrativeAsync(
        DailyStatsDto arena, DailyStatsDto? civic, CancellationToken ct)
    {
        if (!_options.UseLlmNarrative) return null;

        const string system =
            "You are the analyst who writes a one-person product's daily engagement email. " +
            "You are given exact counts for one UTC day across two apps (Arena: AI debates; " +
            "Civic: civic-education exercises). Write a short, plain, honest opener. " +
            "RULES: use ONLY the numbers given — never invent, extrapolate, or estimate. " +
            "Do not congratulate or catastrophize; a zero day is a normal thing to report plainly. " +
            "Call out what CHANGED versus the 7-day average, and anything that went quiet. " +
            "Return ONLY JSON: {\"headline\": \"max 10 words\", \"summary\": \"2-4 sentences\"}.";

        try
        {
            var result = await _llm.GenerateStructuredAsync<Narrative>(
                system, Digest(arena, civic), LlmModelTier.Sonnet, maxTokens: 500, ct: ct);

            var headline = Clamp(result.Headline, 120);
            var summary = Clamp(result.Summary, 900);
            if (string.IsNullOrWhiteSpace(summary)) return null;

            return (headline, summary);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Includes the by-design LlmException when Anthropic:Enabled is false or no key
            // is configured — expected on a paused dev box, not an error worth alerting on.
            _logger.LogInformation(ex, "Daily report: narrative unavailable, using the computed summary.");
            return null;
        }
    }

    /// <summary>Compact, unambiguous rendering of both slices for the model to read.</summary>
    private static string Digest(DailyStatsDto arena, DailyStatsDto? civic)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"UTC day: {arena.Date:yyyy-MM-dd} ({arena.Date.DayOfWeek})");

        void App(DailyStatsDto s)
        {
            var a = s.Audience;
            sb.AppendLine();
            sb.AppendLine($"[{s.App}]");
            sb.AppendLine($"signups={a.Signups} (verified={a.SignupsVerified}, last7={a.SignupsLast7}, total_known_users={a.TotalKnownUsers})");
            sb.AppendLine($"anonymous_arrivals={a.AnonymousArrivals} anonymous_events={a.AnonymousEvents}");
            sb.AppendLine($"active_users={a.ActiveUsers} (yesterday={a.ActiveUsersYesterday})");
            sb.AppendLine("activities (today / users / yesterday / 7day_avg / all_time):");
            foreach (var m in s.Activities)
            {
                sb.AppendLine(
                    $"- {m.Label} [{m.Area}]: {m.Today} / {m.UsersToday} / {m.Yesterday} / " +
                    $"{m.Avg7.ToString("0.##", CultureInfo.InvariantCulture)} / {m.Total}");
            }
        }

        App(arena);
        if (civic is not null) App(civic);
        else sb.AppendLine("\n[civic] stats unavailable for this day.");

        return sb.ToString();
    }

    public static RenderedReport Render(
        DailyStatsDto arena,
        DailyStatsDto? civic,
        bool civicExpected,
        (string Headline, string Summary)? narrative)
    {
        var slices = civic is null ? new[] { arena } : new[] { arena, civic };

        var signups = slices.Sum(s => s.Audience.Signups);
        var active = slices.Sum(s => s.Audience.ActiveUsers);
        var activeYesterday = slices.Sum(s => s.Audience.ActiveUsersYesterday);
        var eventsToday = slices.Sum(s => s.Activities.Sum(a => a.Today));
        var eventsYesterday = slices.Sum(s => s.Activities.Sum(a => a.Yesterday));
        var anonArrivals = slices.Sum(s => s.Audience.AnonymousArrivals);

        var dayLabel = arena.Date.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
        var subject =
            $"Arena daily · {arena.Date.ToString("ddd MMM d", CultureInfo.InvariantCulture)} · " +
            $"{signups} signup{(signups == 1 ? "" : "s")}, {active} active, {N(eventsToday)} events";

        var headline = "What happened";
        var summary = ComputedSummary(signups, active, activeYesterday, eventsToday, eventsYesterday, anonArrivals, slices);
        if (narrative.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(narrative.Value.Headline)) headline = narrative.Value.Headline;
            if (!string.IsNullOrWhiteSpace(narrative.Value.Summary)) summary = narrative.Value.Summary;
        }

        var quiet = slices
            .SelectMany(sl => sl.Activities.Select(a => (App: sl.App, Metric: a)))
            .Where(x => x.Metric.Today == 0 && x.Metric.Avg7 >= 1)
            .OrderByDescending(x => x.Metric.Avg7)
            .ToList();

        // ---------- HTML ----------
        var html = new StringBuilder();
        html.Append("<div style=\"font-family:system-ui,-apple-system,Segoe UI,Arial,sans-serif;max-width:680px;margin:0 auto;color:#111\">");
        html.Append($"<p style=\"color:#666;font-size:12px;margin:0 0 4px\">{E(dayLabel)} · UTC</p>");
        html.Append($"<h2 style=\"margin:0 0 8px;font-size:20px\">{E(headline)}</h2>");
        html.Append($"<p style=\"margin:0 0 18px;line-height:1.55\">{E(summary)}</p>");

        html.Append(HtmlTable(
            "Who showed up",
            new[] { "", "Today", "Yesterday", "Change" },
            slices.SelectMany(sl => new[]
                {
                    Row($"{Title(sl.App)} — signups", sl.Audience.Signups, null),
                    Row($"{Title(sl.App)} — active people", sl.Audience.ActiveUsers, sl.Audience.ActiveUsersYesterday),
                })
                .Concat(arena.Audience.AnonymousArrivals > 0
                    ? new[] { Row("Arena — anonymous arrivals", arena.Audience.AnonymousArrivals, null) }
                    : Array.Empty<string[]>())
                .ToList()));

        foreach (var sl in slices)
        {
            html.Append(HtmlTable(
                $"{Title(sl.App)} activity",
                new[] { "", "Today", "People", "Yesterday", "7-day avg", "All time" },
                sl.Activities.Select(a => new[]
                {
                    a.Label,
                    N(a.Today),
                    a.UsersToday > 0 ? N(a.UsersToday) : "—",
                    N(a.Yesterday),
                    a.Avg7.ToString("0.#", CultureInfo.InvariantCulture),
                    N(a.Total),
                }).ToList()));
        }

        if (quiet.Count > 0)
        {
            html.Append("<h3 style=\"margin:22px 0 6px;font-size:14px\">Went quiet today</h3><ul style=\"margin:0 0 12px;padding-left:18px;color:#444;font-size:13px;line-height:1.6\">");
            foreach (var q in quiet.Take(8))
            {
                html.Append($"<li>{E(Title(q.App))}: {E(q.Metric.Label)} — 0 today, normally {E(q.Metric.Avg7.ToString("0.#", CultureInfo.InvariantCulture))}/day</li>");
            }
            html.Append("</ul>");
        }

        if (civic is null && civicExpected)
        {
            html.Append("<p style=\"margin:14px 0;padding:10px 12px;background:#fef2f2;border-left:3px solid #dc2626;color:#7f1d1d;font-size:13px\">" +
                        "Civic stats could not be fetched for this day — the numbers above cover Arena only.</p>");
        }

        html.Append($"<hr style=\"border:none;border-top:1px solid #eee;margin:22px 0\">" +
                    $"<p style=\"color:#999;font-size:11px;line-height:1.5\">Operator report for the UTC day {E(arena.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}. " +
                    $"Generated {E(DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture))}. " +
                    "Sent because DailyReport:Enabled is true; set it to false to stop.</p></div>");

        // ---------- Text ----------
        var text = new StringBuilder();
        text.AppendLine(dayLabel + " (UTC)");
        text.AppendLine(headline.ToUpperInvariant());
        text.AppendLine();
        text.AppendLine(summary);
        text.AppendLine();
        text.AppendLine("WHO SHOWED UP");
        foreach (var sl in slices)
        {
            text.AppendLine($"  {Title(sl.App)}: {sl.Audience.Signups} signups, {sl.Audience.ActiveUsers} active people " +
                            $"(yesterday {sl.Audience.ActiveUsersYesterday}), {N(sl.Audience.TotalKnownUsers)} known users to date");
        }
        if (arena.Audience.AnonymousArrivals > 0)
            text.AppendLine($"  Arena: {N(arena.Audience.AnonymousArrivals)} anonymous arrivals, {N(arena.Audience.AnonymousEvents)} anonymous events");

        foreach (var sl in slices)
        {
            text.AppendLine();
            text.AppendLine($"{Title(sl.App).ToUpperInvariant()} ACTIVITY (today / people / yesterday / 7-day avg / all time)");
            foreach (var a in sl.Activities)
            {
                text.AppendLine($"  {a.Label}: {N(a.Today)} / {(a.UsersToday > 0 ? N(a.UsersToday) : "-")} / {N(a.Yesterday)} / " +
                                $"{a.Avg7.ToString("0.#", CultureInfo.InvariantCulture)} / {N(a.Total)}");
            }
        }

        if (quiet.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("WENT QUIET TODAY");
            foreach (var q in quiet.Take(8))
                text.AppendLine($"  {Title(q.App)}: {q.Metric.Label} — 0 today, normally {q.Metric.Avg7.ToString("0.#", CultureInfo.InvariantCulture)}/day");
        }

        if (civic is null && civicExpected)
        {
            text.AppendLine();
            text.AppendLine("NOTE: Civic stats could not be fetched for this day — Arena numbers only.");
        }

        text.AppendLine();
        text.AppendLine($"Operator report for the UTC day {arena.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}. " +
                        $"Generated {DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture)}.");

        return new RenderedReport(subject, html.ToString(), text.ToString());
    }

    /// <summary>
    /// The deterministic opener used whenever the LLM one isn't available. Deliberately
    /// says the same things the prose would: scale, direction, and the single biggest mover.
    /// </summary>
    private static string ComputedSummary(
        int signups, int active, int activeYesterday, int eventsToday, int eventsYesterday,
        int anonArrivals, IReadOnlyList<DailyStatsDto> slices)
    {
        var sb = new StringBuilder();
        sb.Append(signups == 0 ? "No new signups." : $"{signups} new signup{(signups == 1 ? "" : "s")}.");
        sb.Append(active == 0
            ? " Nobody signed in did anything tracked."
            : $" {active} known {(active == 1 ? "person" : "people")} active ({Compare(active, activeYesterday)} yesterday).");

        if (anonArrivals > 0)
            sb.Append($" {N(anonArrivals)} anonymous arrival{(anonArrivals == 1 ? "" : "s")}.");

        sb.Append(eventsToday == 0
            ? " No activity recorded across either app."
            : $" {N(eventsToday)} event{(eventsToday == 1 ? "" : "s")} in total ({Compare(eventsToday, eventsYesterday)} yesterday).");

        var top = slices
            .SelectMany(sl => sl.Activities.Select(a => (sl.App, Metric: a)))
            .Where(x => x.Metric.Today > 0)
            .OrderByDescending(x => x.Metric.Today)
            .FirstOrDefault();

        if (top.Metric is not null)
            sb.Append($" Busiest: {top.Metric.Label.ToLowerInvariant()} ({N(top.Metric.Today)}) in {Title(top.App)}.");

        return sb.ToString();
    }

    private static string Compare(int today, int yesterday)
    {
        if (yesterday == today) return "same as";
        var diff = today - yesterday;
        return diff > 0 ? $"up {N(diff)} from" : $"down {N(-diff)} from";
    }

    private static string[] Row(string label, int today, int? yesterday)
    {
        var change = yesterday is null ? "—" : Signed(today - yesterday.Value);
        return new[] { label, N(today), yesterday is null ? "—" : N(yesterday.Value), change };
    }

    private static string Signed(int diff) => diff switch
    {
        0 => "—",
        > 0 => $"+{N(diff)}",
        _ => $"-{N(-diff)}",
    };

    private static string HtmlTable(string caption, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.Append($"<h3 style=\"margin:22px 0 6px;font-size:14px\">{E(caption)}</h3>");
        sb.Append("<table style=\"border-collapse:collapse;width:100%;font-size:13px\"><thead><tr>");
        for (var i = 0; i < headers.Count; i++)
        {
            var align = i == 0 ? "left" : "right";
            sb.Append($"<th style=\"text-align:{align};padding:6px 8px;border-bottom:2px solid #e5e5e5;color:#666;font-weight:600\">{E(headers[i])}</th>");
        }
        sb.Append("</tr></thead><tbody>");

        foreach (var row in rows)
        {
            sb.Append("<tr>");
            for (var i = 0; i < row.Length; i++)
            {
                var align = i == 0 ? "left" : "right";
                var weight = i == 1 ? "600" : "400";
                sb.Append($"<td style=\"text-align:{align};padding:6px 8px;border-bottom:1px solid #f0f0f0;font-weight:{weight}\">{E(row[i])}</td>");
            }
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    private static string Title(string app) => app switch
    {
        "arena" => "Arena",
        "civic" => "Civic",
        _ => app,
    };

    private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>HTML-encode. Applied to every interpolated value including the model-written
    /// narrative, so generated prose can never inject markup into the email.</summary>
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string Clamp(string? value, int max)
    {
        var s = (value ?? "").Trim();
        return s.Length <= max ? s : s[..max].TrimEnd() + "…";
    }
}
