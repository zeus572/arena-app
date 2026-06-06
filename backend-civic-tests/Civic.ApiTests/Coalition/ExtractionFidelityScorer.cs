using Civic.API.Services.Coalition;

namespace Civic.ApiTests.Coalition;

public record CaseScore(
    string Id,
    int GoldPositions,
    int MatchedPositions,
    bool NewSubQExpected,
    bool NewSubQFired,
    bool NewSubQCorrect,
    IReadOnlyList<string> Mismatches);

public record FidelityReport(
    int TotalGoldPositions,
    int TotalMatchedPositions,
    double PositionAccuracy,
    int NewSubQCasesCorrect,
    int NewSubQCasesTotal,
    IReadOnlyList<CaseScore> CaseScores)
{
    public bool NewSubQDetectionPerfect => NewSubQCasesCorrect == NewSubQCasesTotal;
}

/// <summary>
/// Scores extraction output against the hand-labeled corpus. Pure logic so it can
/// be unit-tested offline (without a live LLM) and reused by the live fidelity
/// test once an API key is available.
///
/// - Position accuracy = (gold positions extraction got right) / (all gold positions).
///   A gold (key -> label) is "right" when the extraction resolved that key to the
///   same label (case-insensitive, trimmed).
/// - New-sub-question detection = did extraction surface a new sub-question
///   exactly on the cases where the label says it should (and not otherwise)?
/// </summary>
public static class ExtractionFidelityScorer
{
    private static string Norm(string s) => s.Trim().ToLowerInvariant();

    public static CaseScore ScoreCase(FidelityCase c, ExtractionResult result)
    {
        var mismatches = new List<string>();
        var matched = 0;
        foreach (var (key, goldLabel) in c.GoldPositions)
        {
            if (result.Positions.TryGetValue(key, out var got) && Norm(got) == Norm(goldLabel))
            {
                matched++;
            }
            else
            {
                var gotDesc = result.Positions.TryGetValue(key, out var g) ? $"'{g}'" : "(absent)";
                mismatches.Add($"{c.Id}: {key} expected '{goldLabel}' but got {gotDesc}");
            }
        }

        var fired = result.NewSubQuestions.Count > 0;
        var newCorrect = fired == c.ExpectsNewSubQuestion;
        if (!newCorrect)
        {
            mismatches.Add($"{c.Id}: new-sub-question expected={c.ExpectsNewSubQuestion} but fired={fired}");
        }

        return new CaseScore(c.Id, c.GoldPositions.Count, matched,
            c.ExpectsNewSubQuestion, fired, newCorrect, mismatches);
    }

    public static FidelityReport Score(IReadOnlyList<(FidelityCase Case, ExtractionResult Result)> runs)
    {
        var scores = runs.Select(r => ScoreCase(r.Case, r.Result)).ToList();
        var totalGold = scores.Sum(s => s.GoldPositions);
        var totalMatched = scores.Sum(s => s.MatchedPositions);
        var accuracy = totalGold == 0 ? 1.0 : (double)totalMatched / totalGold;
        var newCorrect = scores.Count(s => s.NewSubQCorrect);
        return new FidelityReport(totalGold, totalMatched, accuracy, newCorrect, scores.Count, scores);
    }
}
