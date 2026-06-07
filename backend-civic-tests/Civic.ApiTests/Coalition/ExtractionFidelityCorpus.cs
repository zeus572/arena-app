using Civic.API.Models;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// One hand-labeled free-form version: the known sub-questions in play, the
/// version text, the GOLD resolved positions a human reader assigns, and whether
/// the text should make extraction surface a brand-new sub-question (A4).
/// </summary>
public record FidelityCase(
    string Id,
    string ProvisionKey,
    IReadOnlyList<SubQuestion> Known,
    string VersionText,
    IReadOnlyDictionary<string, string> GoldPositions,
    bool ExpectsNewSubQuestion);

/// <summary>
/// STARTER extraction-fidelity corpus (Phase 0.3). ~15 hand-labeled versions
/// across the four sample provisions, including cases that should surface a NEW
/// sub-question. This is deliberately small and is the seed of a corpus the
/// HUMAN is expected to expand continuously (07 plan, Part E: "the single most
/// important test asset").
///
/// Labels are the author's reading of each text; they are the ground truth the
/// live extraction is scored against.
/// </summary>
public static class ExtractionFidelityCorpus
{
    // ---- known sub-question sets per sample provision (subset used for scoring) ----

    private static SubQuestion SQ(string key, string prompt, params string[] options) =>
        new() { Key = key, Prompt = prompt, PositionOptions = options };

    private static IReadOnlyList<SubQuestion> StudentData() => new[]
    {
        SQ("floor-vs-ceiling", "Is the national standard a floor (states may exceed) or a ceiling that preempts local rules?", "floor", "ceiling"),
        SQ("enforcement-authority", "Who enforces the standard?", "federal-agency", "state-ag", "private-right-of-action"),
        SQ("retention-limit", "How long may student data be retained?", "delete-on-graduation", "fixed-years", "vendor-discretion"),
        SQ("vendor-scope", "Which vendors are covered?", "all-vendors", "large-only", "k12-contracted-only"),
    };

    private static IReadOnlyList<SubQuestion> OnlineSpeech() => new[]
    {
        SQ("platform-scope", "Which platforms are covered?", "large-only", "all-platforms", "dominant-gatekeepers-only"),
        SQ("transparency-vs-must-carry", "Transparency/consistency only, or also a carriage mandate?", "transparency-only", "transparency-plus-appeal", "must-carry"),
        SQ("enforcer", "Who enforces and adjudicates disputes?", "federal-agency", "independent-board", "courts-only"),
    };

    private static IReadOnlyList<SubQuestion> AiHiring() => new[]
    {
        SQ("employer-scope", "Which employers are covered?", "all-employers", "size-threshold", "federal-contractors-only"),
        SQ("explanation-depth", "How detailed must the explanation be?", "main-factors", "full-feature-weights", "category-level-only"),
        SQ("audit-requirement", "Must tools be independently audited for bias?", "mandatory-third-party", "self-audit-attestation", "none"),
        SQ("human-review-trigger", "When is human review guaranteed?", "every-rejection", "on-request", "high-stakes-roles-only"),
    };

    private static IReadOnlyList<SubQuestion> SchoolPhones() => new[]
    {
        SQ("time-scope", "Bell-to-bell or only during instruction?", "bell-to-bell", "instruction-only"),
        SQ("opt-out-holder", "Who can override the default?", "district", "school", "parent"),
        SQ("emergency-exception", "What emergency/medical access is guaranteed?", "medical-and-emergency", "emergency-only", "none-codified"),
        SQ("enforcement", "How is the limit enforced?", "confiscation", "graduated-penalties", "honor-system"),
    };

    private static Dictionary<string, string> P(params (string k, string v)[] pairs) =>
        pairs.ToDictionary(x => x.k, x => x.v);

    public static IReadOnlyList<FidelityCase> Cases { get; } = new List<FidelityCase>
    {
        // ---- Student data privacy ----
        new("SD-1", "student-data", StudentData(),
            "Set a national floor that states are free to exceed, and have the FTC enforce it.",
            P(("floor-vs-ceiling", "floor"), ("enforcement-authority", "federal-agency")), false),
        new("SD-2", "student-data", StudentData(),
            "A single national standard that overrides any conflicting state rule, enforced by state attorneys general, with student data deleted when a student graduates.",
            P(("floor-vs-ceiling", "ceiling"), ("enforcement-authority", "state-ag"), ("retention-limit", "delete-on-graduation")), false),
        new("SD-3", "student-data", StudentData(),
            "Let families sue education-technology vendors directly when their child's data is misused.",
            P(("enforcement-authority", "private-right-of-action")), false),
        new("SD-4", "student-data", StudentData(),
            "Apply the baseline only to the largest education-technology platforms; small classroom tools are exempt.",
            P(("vendor-scope", "large-only")), false),
        new("SD-5-NEW", "student-data", StudentData(),
            "Keep a national floor states can exceed, but require that parents be able to opt their child out of all non-essential data sharing entirely.",
            P(("floor-vs-ceiling", "floor")), true), // surfaces a parental-opt-out crux not in the known set

        // ---- Online speech / platform moderation ----
        new("OS-1", "online-speech", OnlineSpeech(),
            "Only the largest platforms must publish their moderation rules and apply them consistently, with an appeal path for affected users and no carriage mandate. An independent board hears appeals.",
            P(("platform-scope", "large-only"), ("transparency-vs-must-carry", "transparency-plus-appeal"), ("enforcer", "independent-board")), false),
        new("OS-2", "online-speech", OnlineSpeech(),
            "Every platform, large or small, must carry any lawful speech a user posts.",
            P(("platform-scope", "all-platforms"), ("transparency-vs-must-carry", "must-carry")), false),
        new("OS-3", "online-speech", OnlineSpeech(),
            "Disputes over moderation should be resolved only in the courts, not by any agency.",
            P(("enforcer", "courts-only")), false),
        new("OS-4-NEW", "online-speech", OnlineSpeech(),
            "Large platforms must let users choose among competing third-party moderation filters layered on top of the platform.",
            P(("platform-scope", "large-only")), true), // surfaces a user-selectable-middleware crux

        // ---- AI in hiring ----
        new("AI-1", "ai-hiring", AiHiring(),
            "Employers with more than 100 staff that use AI screening must disclose the main factors the tool weighs and allow a candidate to request human review.",
            P(("employer-scope", "size-threshold"), ("explanation-depth", "main-factors"), ("human-review-trigger", "on-request")), false),
        new("AI-2", "ai-hiring", AiHiring(),
            "Require independent third-party bias audits of every employer's hiring algorithm.",
            P(("audit-requirement", "mandatory-third-party"), ("employer-scope", "all-employers")), false),
        new("AI-3", "ai-hiring", AiHiring(),
            "Cover only federal contractors, and let them self-attest that their tools are unbiased.",
            P(("employer-scope", "federal-contractors-only"), ("audit-requirement", "self-audit-attestation")), false),
        new("AI-4-NEW", "ai-hiring", AiHiring(),
            "A rejected candidate may demand the same role be re-scored by a different vendor's independent tool.",
            P(), true), // surfaces a second-tool re-evaluation crux; silent on the known sub-questions

        // ---- School phones ----
        new("SP-1", "school-phones", SchoolPhones(),
            "Phones off bell-to-bell; districts may opt out; medical and emergency access is guaranteed.",
            P(("time-scope", "bell-to-bell"), ("opt-out-holder", "district"), ("emergency-exception", "medical-and-emergency")), false),
        new("SP-2", "school-phones", SchoolPhones(),
            "Limit phones only during instructional time; parents may override the policy; enforce with graduated penalties.",
            P(("time-scope", "instruction-only"), ("opt-out-holder", "parent"), ("enforcement", "graduated-penalties")), false),
        new("SP-3-NEW", "school-phones", SchoolPhones(),
            "Phones off bell-to-bell, and the state must fund lockable phone-storage pouches for every school.",
            P(("time-scope", "bell-to-bell")), true), // surfaces a who-funds-storage crux
    };
}
