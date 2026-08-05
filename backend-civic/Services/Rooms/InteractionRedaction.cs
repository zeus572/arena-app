using System.Text.Json.Nodes;
using Civic.API.Models.Rooms;

namespace Civic.API.Services.Rooms;

/// <summary>
/// Strips answer keys before a payload leaves the server, mirroring DailyRedaction.
///
/// The rule is allow-list, not deny-list: the redacted payload is REBUILT from the fields a
/// player is allowed to see, rather than copied and then scrubbed. A deny-list quietly
/// leaks every field someone adds later and forgets to add to it, and the failure is
/// invisible until someone reads the network tab.
/// </summary>
public static class InteractionRedaction
{
    /// <summary>Field names that must never reach a client. Asserted by a test.</summary>
    public static readonly IReadOnlySet<string> ForbiddenKeys = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "correctOptionId",
        "correctLabel",
        "trueOrder",
        "knowabilityNotes",
    };

    public static JsonNode? ForPlayer(InteractionKind kind, string payloadJson)
    {
        var payload = JsonNode.Parse(payloadJson);
        if (payload is null) return null;

        return kind switch
        {
            InteractionKind.BeforeYouKnow => RedactBeforeYouKnow(payload),
            InteractionKind.ClassifyStatement => RedactClassify(payload),
            InteractionKind.TimelineBuilder => RedactTimeline(payload),
            InteractionKind.VoteBeforeReading => payload, // carries no answer key
            InteractionKind.CalibratedPrediction => payload, // just a prediction id
            // An unhandled kind returns nothing rather than leaking a payload it does not
            // know how to redact. Failing closed is the only safe default here.
            _ => null,
        };
    }

    private static JsonNode RedactBeforeYouKnow(JsonNode payload)
    {
        var options = new JsonArray();
        foreach (var opt in payload["options"]?.AsArray() ?? new JsonArray())
        {
            options.Add(new JsonObject
            {
                ["id"] = opt?["id"]?.GetValue<string>(),
                ["text"] = opt?["text"]?.GetValue<string>(),
                // Explanations are withheld until the reveal.
            });
        }

        return new JsonObject
        {
            ["question"] = payload["question"]?.GetValue<string>(),
            ["options"] = options,
        };
    }

    private static JsonNode RedactClassify(JsonNode payload)
    {
        var items = new JsonArray();
        foreach (var item in payload["items"]?.AsArray() ?? new JsonArray())
        {
            items.Add(new JsonObject
            {
                ["id"] = item?["id"]?.GetValue<string>(),
                ["text"] = item?["text"]?.GetValue<string>(),
                ["sourceRefId"] = item?["sourceRefId"]?.GetValue<Guid?>(),
            });
        }

        return new JsonObject
        {
            ["items"] = items,
            ["labels"] = new JsonArray("Factual", "Interpretation", "Opinion", "Prediction"),
        };
    }

    private static JsonNode RedactTimeline(JsonNode payload)
    {
        var ids = new JsonArray();
        foreach (var id in payload["eventIds"]?.AsArray() ?? new JsonArray())
        {
            ids.Add(id?.GetValue<string>());
        }

        // eventIds is the shuffled pool; trueOrder and the knowability notes are the answer.
        return new JsonObject { ["eventIds"] = ids };
    }
}
