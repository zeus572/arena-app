using System.Text.Json;
using System.Text.Json.Nodes;

namespace Civic.API.Models.Daily;

/// <summary>
/// Typed payload + response contracts for the six daily games. Each payload is
/// persisted whole (answer key included) on <see cref="DailyPuzzle.PayloadJson"/> and
/// redacted by <see cref="DailyRedaction"/> before it is served to a client.
///
/// Payload shapes are documented per game in docs/civic_daily_games/. Bump
/// <see cref="DailyPuzzle.PayloadVersion"/> when a shape changes incompatibly.
/// </summary>
public static class DailyJson
{
    /// <summary>
    /// camelCase both ways so stored payloads read the same as the wire format and the
    /// spec examples. Used for every payload/response serialize + deserialize.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} payload.");
}

// ---------------------------------------------------------------- Fork (01)

public record ForkOption(string Label, string Cost);

public record ForkPayload(
    string Question,
    string Tradeoff,
    ForkOption OptionA,
    ForkOption OptionB,
    string AxisKey,
    string SubQuestionKey,
    string? ProvisionSlug);

public record ForkResponse(string Choice);

// ---------------------------------------------------------- Crowd Call (02)

/// <summary>Where a round's "true rate" comes from. Must always be shown to the player.</summary>
public static class CrowdSource
{
    public const string CivicUsers = "civic-users";
    public const string NationalPoll = "national-poll";
}

public record CrowdCallRound(
    string Prompt,
    string Answer,
    string Explanation,
    string CrowdSource,
    string Attribution,
    string? SourceUrl,
    string? FieldedOn,
    int SampleSize,
    double TrueRate);

public record CrowdCallPayload(List<CrowdCallRound> Rounds);

public record CrowdCallResponse(List<double> Guesses);

// ----------------------------------------------------------- Priced In (03)

public record PricedInPayload(
    string Prompt,
    string Unit,
    double MinBound,
    double MaxBound,
    int MaxGuesses,
    double TrueValue,
    string Anchor,
    string Source,
    string? SourceUrl,
    string? AsOf);

/// <summary>One guess in the higher/lower ladder. <c>Final</c> ends the play.</summary>
public record PricedInGuessRequest(double Guess, bool Final);

public record PricedInResponse(List<double> Guesses);

// ------------------------------------------------------------ Place It (04)

public record PlaceItAxis(
    string AxisKey,
    string Name,
    string LowLabel,
    string HighLabel,
    int TrueBucket,
    string Rationale,
    string? Evidence);

public record PlaceItPayload(
    Guid BillId,
    string BillTitle,
    string BillSummary,
    string BillStatus,
    List<PlaceItAxis> Axes,
    int MaxRounds);

public record PlaceItResponse(List<List<int>> Rounds);

// -------------------------------------------------------- Time Machine (05)

public static class TimeMachineMode
{
    public const string Sort = "sort";
    public const string OddOneOut = "oddOneOut";
}

public record TimeMachineItem(string Id, string Headline, string Publisher);

public record TimeMachinePayload(
    string Mode,
    List<TimeMachineItem> Items,
    List<string> TrueOrder,
    string? CurrentItemId,
    Dictionary<string, string> Dates,
    Dictionary<string, string> Urls,
    string RevealLine);

public record TimeMachineResponse(List<string>? Order, string? Pick);

// --------------------------------------------------------- Whose Value (06)

public record WhoseValueChoice(string AxisKey, string Name, string LowLabel, string HighLabel);

public record WhoseValueRound(
    string Argument,
    string BillTitle,
    Guid BillId,
    List<WhoseValueChoice> Choices,
    string CorrectAxisKey);

public record WhoseValuePayload(List<WhoseValueRound> Rounds);

public record WhoseValueResponse(List<string> Picks);

// ------------------------------------------------------- Which Is True (07)

/// <summary>Which content family a round was cut from. Shown to the player as a kicker.</summary>
public static class WhichIsTrueTopic
{
    public const string FederalBudget = "Federal budget";
    public const string StateAndLocalTax = "State & local tax";
    public const string Congress = "Congress";
}

/// <summary>
/// One round: a question and two options, exactly one of which answers it.
///
/// The invariant that makes this game honest — enforced by the generator and asserted in
/// <c>DailyContentTests</c> — is that BOTH options are real. The decoy is always another
/// true figure from the same family (a different state, a different filing status, a
/// different bill), never a fabricated number. <see cref="DecoyTruth"/> is what the loser
/// actually is, and the reveal always says so.
/// </summary>
public record WhichIsTrueRound(
    /// <summary>Stable dedup key ("state-sales:OH"). Bookkeeping — the client never needs it.</summary>
    string Key,
    string Topic,
    string Prompt,
    string OptionA,
    string OptionB,
    /// <summary>"A" | "B".</summary>
    string Correct,
    string Explanation,
    /// <summary>What the option the player didn't want really is.</summary>
    string DecoyTruth,
    string Source,
    string? SourceUrl,
    string? AsOf,
    Guid? BillId);

public record WhichIsTruePayload(List<WhichIsTrueRound> Rounds);

public record WhichIsTrueResponse(List<string> Picks);

// ------------------------------------------------------------- Redaction

/// <summary>
/// Strips answer-key fields from a payload before it goes to a client. This is the
/// single source of truth for which fields are secret — the GET path never serializes
/// a raw payload, and every game spec's "SECRET — strip on GET" markers correspond to
/// an entry here.
///
/// Paths are simple: "field" for a root property, "array[].field" for a property of
/// every element of a root array.
/// </summary>
public static class DailyRedaction
{
    private static readonly Dictionary<DailyGameKind, string[]> SecretPaths = new()
    {
        // Fork has no answer key — nothing to hide.
        [DailyGameKind.Fork] = Array.Empty<string>(),

        [DailyGameKind.CrowdCall] = new[]
        {
            "rounds[].trueRate",
            "rounds[].sampleSize",
        },

        [DailyGameKind.PricedIn] = new[]
        {
            "trueValue",
            "anchor",
        },

        [DailyGameKind.PlaceIt] = new[]
        {
            "axes[].trueBucket",
            "axes[].rationale",
            "axes[].evidence",
        },

        [DailyGameKind.TimeMachine] = new[]
        {
            "trueOrder",
            "currentItemId",
            "dates",
        },

        [DailyGameKind.WhoseValue] = new[]
        {
            "rounds[].correctAxisKey",
            "rounds[].billTitle",
            "rounds[].billId",
        },

        // Which Is True redacts its provenance too, which the other games don't. With only
        // two options on the card, a citation IS the answer key: "ssa.gov/oact/cola" or
        // "H.R. 1234 (118th Congress)" hands over the figure the question is asking for.
        // Source, url and as-of all come back in the reveal, where they belong.
        [DailyGameKind.WhichIsTrue] = new[]
        {
            "rounds[].key",
            "rounds[].correct",
            "rounds[].explanation",
            "rounds[].decoyTruth",
            "rounds[].source",
            "rounds[].sourceUrl",
            "rounds[].asOf",
            "rounds[].billId",
        },
    };

    /// <summary>Returns the payload as a JsonNode with every secret field removed.</summary>
    public static JsonNode Redact(DailyGameKind kind, string payloadJson)
    {
        var node = JsonNode.Parse(payloadJson)
            ?? throw new InvalidOperationException("Puzzle payload is not valid JSON.");

        foreach (var path in SecretPaths[kind]) Remove(node, path);
        return node;
    }

    /// <summary>The secret field paths for a kind — exposed so tests can assert on them.</summary>
    public static IReadOnlyList<string> PathsFor(DailyGameKind kind) => SecretPaths[kind];

    private static void Remove(JsonNode root, string path)
    {
        var arrayMarker = path.IndexOf("[].", StringComparison.Ordinal);
        if (arrayMarker < 0)
        {
            (root as JsonObject)?.Remove(path);
            return;
        }

        var arrayName = path[..arrayMarker];
        var field = path[(arrayMarker + 3)..];
        if (root is not JsonObject obj || obj[arrayName] is not JsonArray array) return;

        foreach (var element in array)
            (element as JsonObject)?.Remove(field);
    }
}
