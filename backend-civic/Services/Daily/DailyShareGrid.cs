using System.Text;
using Civic.API.Models.Daily;

namespace Civic.API.Services.Daily;

/// <summary>
/// Builds the copyable emoji grid — the Wordle growth engine. Built server-side so it
/// is byte-identical on web and Android.
///
/// Hard rule: a grid conveys PROGRESS, never the SOLUTION. No headline, axis name,
/// figure, or true rate may appear in a grid; anyone who hasn't played yet must learn
/// nothing from seeing one.
/// </summary>
public static class DailyShareGrid
{
    public const string ShareUrl = "civersify.com/daily";

    private static string Square(string band) => band switch
    {
        Bands.Hit => "🟩",
        Bands.Near => "🟨",
        _ => "🟥",
    };

    private static string Title(DailyGameKind kind, int edition) => kind switch
    {
        DailyGameKind.Fork => $"Fork #{edition}",
        DailyGameKind.CrowdCall => $"Crowd Call #{edition}",
        DailyGameKind.PricedIn => $"Priced In #{edition}",
        DailyGameKind.PlaceIt => $"Place It #{edition}",
        DailyGameKind.TimeMachine => $"Time Machine #{edition}",
        DailyGameKind.WhoseValue => $"Whose Value #{edition}",
        _ => $"Civersify #{edition}",
    };

    private static string Wrap(string title, IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        foreach (var line in lines) sb.AppendLine(line);
        sb.Append(ShareUrl);
        return sb.ToString();
    }

    /// <summary>Fork has no score — the hook is the split, which teases without spoiling.</summary>
    public static string Fork(int edition, string choice, int otherSharePercent)
    {
        var other = choice == "A" ? "B" : "A";
        return Wrap(Title(DailyGameKind.Fork, edition), new[]
        {
            $"◧ I went {choice} — {otherSharePercent}% of the country went {other}.",
        });
    }

    public static string CrowdCall(int edition, int score, IEnumerable<RoundResult> rounds, int overestimated, int total)
    {
        var squares = string.Concat(rounds.Select(r => Square(r.Band)));
        return Wrap($"{Title(DailyGameKind.CrowdCall, edition)} — {score}/100", new[]
        {
            squares,
            $"I overestimated division on {overestimated} of {total}.",
        });
    }

    public static string PricedIn(int edition, int guessesUsed, double closeness)
    {
        var within = closeness switch
        {
            <= 1.25 => "within 1.25x",
            <= 2 => "within 2x",
            <= 5 => "within 5x",
            <= 10 => "within 10x",
            _ => "way off",
        };
        return Wrap(Title(DailyGameKind.PricedIn, edition), new[]
        {
            $"🎯 Got it in {guessesUsed} — {within}",
        });
    }

    /// <summary>One row per round, one square per axis. Axis names are NOT in the grid —
    /// that would leak which axes are in play to anyone who hasn't played.</summary>
    public static string PlaceIt(int edition, IEnumerable<IEnumerable<RoundResult>> roundRows) =>
        Wrap(Title(DailyGameKind.PlaceIt, edition),
            roundRows.Select(row => string.Concat(row.Select(r => Square(r.Band)))));

    public static string TimeMachineSort(int edition, int concordant, int totalPairs, IEnumerable<RoundResult> slots) =>
        Wrap($"{Title(DailyGameKind.TimeMachine, edition)}", new[]
        {
            $"Sort — {concordant}/{totalPairs} pairs",
            string.Concat(slots.Select(s => Square(s.Band))),
        });

    public static string TimeMachineOddOneOut(int edition, bool correct) =>
        Wrap(Title(DailyGameKind.TimeMachine, edition), new[]
        {
            "Odd one out",
            correct ? "🟩" : "🟥",
        });

    public static string WhoseValue(int edition, int correct, int total, string? sharpestAxisName, IEnumerable<RoundResult> rounds)
    {
        var header = sharpestAxisName is null
            ? $"{Title(DailyGameKind.WhoseValue, edition)} — {correct}/{total}"
            : $"{Title(DailyGameKind.WhoseValue, edition)} — {correct}/{total}, sharpest on {sharpestAxisName}";
        return Wrap(header, new[] { string.Concat(rounds.Select(r => Square(r.Band))) });
    }
}
