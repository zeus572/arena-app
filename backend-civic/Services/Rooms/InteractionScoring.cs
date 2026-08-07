using Civic.API.Models.Rooms;

namespace Civic.API.Services.Rooms;

/// <summary>Per-item feedback. Every item gets an explanation, right or wrong.</summary>
public record ItemResult(string ItemId, bool Correct, string Explanation, string? CorrectLabel = null);

/// <summary>What the server sends back after a response.</summary>
public record InteractionResult(
    int Score,
    bool Scored,
    string Explanation,
    IReadOnlyList<ItemResult> Items);

/// <summary>
/// Scoring for the MVP interactions. Pure.
///
/// Two of the five are deliberately UNSCORED. Before You Know is a commitment device, not
/// a quiz, and Vote Before Reading asks for a policy preference — scoring either would
/// hand the product an ideological answer key, which PRD 06 §8 forbids outright.
/// </summary>
public static class InteractionScoring
{
    public static InteractionResult ScoreBeforeYouKnow(
        BeforeYouKnowPayload payload, BeforeYouKnowResponse response)
    {
        var chosen = payload.Options.FirstOrDefault(o => o.Id == response.OptionId);

        // Every option's explanation is returned, not just the chosen one — the point is to
        // show why the tempting wrong answers are tempting.
        var items = payload.Options
            .Select(o => new ItemResult(
                o.Id,
                Correct: payload.CorrectOptionId is not null && o.Id == payload.CorrectOptionId,
                o.Explanation))
            .ToList();

        return new InteractionResult(
            Score: 0,
            Scored: false,
            Explanation: chosen is null ? payload.RevealText : payload.RevealText,
            Items: items);
    }

    /// <summary>
    /// Partial credit across items. An interaction that only says "3 of 5" teaches less
    /// than one that says which two, and why.
    /// </summary>
    public static InteractionResult ScoreClassifyStatement(
        ClassifyStatementPayload payload, ClassifyStatementResponse response)
    {
        var items = new List<ItemResult>();
        var correct = 0;

        foreach (var item in payload.Items)
        {
            response.Labels.TryGetValue(item.Id, out var given);
            var ok = string.Equals(given, item.CorrectLabel, StringComparison.OrdinalIgnoreCase);
            if (ok) correct++;

            items.Add(new ItemResult(item.Id, ok, item.Explanation, item.CorrectLabel));
        }

        var score = payload.Items.Count == 0
            ? 0
            : (int)Math.Round(100.0 * correct / payload.Items.Count);

        return new InteractionResult(score, Scored: true, Explanation: "", Items: items);
    }

    /// <summary>
    /// Ordering score by concordant pairs (Kendall tau, rescaled to 0..100).
    ///
    /// Pairwise rather than position-matching because getting one early event wrong should
    /// not cascade into marking everything after it wrong — the reader's understanding of
    /// the sequence is mostly right, and the score should say so.
    /// </summary>
    public static InteractionResult ScoreTimelineBuilder(
        TimelineBuilderPayload payload, TimelineBuilderResponse response)
    {
        var truth = payload.TrueOrder;
        var given = response.Order;

        var rank = truth.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);

        var comparable = 0;
        var concordant = 0;

        for (var a = 0; a < given.Count; a++)
        {
            for (var b = a + 1; b < given.Count; b++)
            {
                if (!rank.TryGetValue(given[a], out var ra)) continue;
                if (!rank.TryGetValue(given[b], out var rb)) continue;

                comparable++;
                if (ra < rb) concordant++;
            }
        }

        var score = comparable == 0 ? 0 : (int)Math.Round(100.0 * concordant / comparable);

        // The payoff pass: what was knowable on each date, in true order.
        var items = truth
            .Where(payload.KnowabilityNotes.ContainsKey)
            .Select(id => new ItemResult(
                id,
                Correct: given.Count > 0 && given.IndexOf(id) == truth.IndexOf(id),
                payload.KnowabilityNotes[id]))
            .ToList();

        return new InteractionResult(score, Scored: true, Explanation: "", Items: items);
    }

    /// <summary>
    /// Unscored by design. A policy preference has no correct answer, and PRD 06 §8
    /// forbids scoring one as though it did.
    /// </summary>
    public static InteractionResult ScoreVoteBeforeReading(VoteBeforeReadingResponse _)
        => new(0, Scored: false, Explanation: "", Items: Array.Empty<ItemResult>());

    /// <summary>Whether a kind produces a score at all.</summary>
    public static bool IsScored(InteractionKind kind) => kind switch
    {
        InteractionKind.BeforeYouKnow => false,
        InteractionKind.VoteBeforeReading => false,
        InteractionKind.ClassifyStatement => true,
        InteractionKind.TimelineBuilder => true,
        // Calibrated predictions are scored by Brier, at resolution, not at play time.
        InteractionKind.CalibratedPrediction => false,
        _ => false,
    };
}
