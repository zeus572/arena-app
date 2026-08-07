using System.Text.Json;
using System.Text.Json.Serialization;

namespace Civic.API.Services.Rooms;

/// <summary>
/// Payload shapes for the five MVP interaction kinds, mirroring Models/Daily/Payloads.cs.
///
/// Every one of these carries its answer key AND its explanations. PRD 06 is explicit that
/// correct answers must not depend on a live model call at play time — they are
/// pre-generated, reviewed and versioned — so everything the server needs to score and
/// explain a response lives in the row.
/// </summary>
public static class InteractionJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T? Parse<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}

// ---------------------------------------------------------------- Before You Know

/// <param name="Id">Stable option id; responses reference this, never the index.</param>
/// <param name="Explanation">Shown whether or not this option was picked.</param>
public record BykOption(string Id, string Text, string Explanation);

/// <param name="CorrectOptionId">Null when the question has no right answer.</param>
public record BeforeYouKnowPayload(
    string Question,
    List<BykOption> Options,
    string? CorrectOptionId,
    string RevealText);

public record BeforeYouKnowResponse(string OptionId);

// ---------------------------------------------------------------- Classify statement

/// <param name="Text">Pulled VERBATIM from real coverage — never paraphrased.</param>
/// <param name="ClaimId">Set when the correct label depends on a claim's current status.</param>
public record ClassifyItem(
    string Id,
    string Text,
    string CorrectLabel,
    string Explanation,
    Guid? SourceRefId = null,
    Guid? ClaimId = null);

public record ClassifyStatementPayload(List<ClassifyItem> Items);

public record ClassifyStatementResponse(Dictionary<string, string> Labels);

// ---------------------------------------------------------------- Timeline builder

/// <param name="TrueOrder">Event ids in their real chronological order.</param>
/// <param name="KnowabilityNotes">
/// Event id to "what was knowable on this date". The second pass — the whole payoff — shows
/// that most confident takes predate the evidence that contradicted them.
/// </param>
/// <param name="Labels">
/// Event id to the text shown on the card. Sent to the player — an id is not a label, and
/// without this the client can only render slugs. The ANSWER is the order, not the wording,
/// so passing labels through leaks nothing.
/// </param>
public record TimelineBuilderPayload(
    List<string> EventIds,
    List<string> TrueOrder,
    Dictionary<string, string> KnowabilityNotes,
    Dictionary<string, string>? Labels = null);

public record TimelineBuilderResponse(List<string> Order);

// ---------------------------------------------------------------- Vote before reading

public record VoteBeforeReadingPayload(
    string Question,
    List<string> ArgumentsForClaimIds,
    List<string> ArgumentsAgainstClaimIds);

/// <param name="Vote">"Yes" | "No" | "NotSure".</param>
public record VoteBeforeReadingResponse(string Vote);

// ---------------------------------------------------------------- Calibrated prediction

/// <summary>A thin pointer. The answer lives in UserPrediction, not in a play row.</summary>
public record CalibratedPredictionPayload(Guid PredictionId);
