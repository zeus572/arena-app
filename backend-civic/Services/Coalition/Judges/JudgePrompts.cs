namespace Civic.API.Services.Coalition.Judges;

/// <summary>(System, User) prompt pairs for the coalition judges. Bounded, JSON-only.</summary>
public static class JudgePrompts
{
    private const string Rules = "Respond with ONLY a single JSON object. No prose, no markdown fences.";

    public static (string System, string User) Governance(string text, IEnumerable<string> axes) =>
    (
        $$"""
        You score a civic contribution on TWO axes for a coalition-building game.
        - governance (0-100): does it operate at the level of institutions, mechanisms, tradeoffs,
          and implementable action (high) or identity/cultural signaling (low)? Reward the descent
          into governance; do NOT punish values talk, just score where it sits.
        - reasoningQuality (0-100): is it specific, engages tradeoffs, non-tribal?
        - layer: "governance" or "culture".
        {{Rules}}
        Shape: {"governance": <int>, "reasoningQuality": <int>, "layer": "governance|culture"}
        """,
        $$"""
        Relevant Values axes: {{string.Join(", ", axes)}}
        Contribution:
        ---
        {{text}}
        ---
        Score it.
        """
    );

    public static (string System, string User) CommonGround(string statement) =>
    (
        $$"""
        You judge whether a proposed cross-spectrum agreement is REAL, not platitude civility.
        It counts only if it is concrete (names a specific policy/principle), costly (signers gave up
        a maximalist position), and cross-cutting (genuinely spans the spectrum). "NAFTA was bad for
        workers" beats "we both love America".
        {{Rules}}
        Shape: {"isGenuine": <bool>, "concrete": <bool>, "costly": <bool>, "crossCutting": <bool>, "reason": "<short>"}
        """,
        $$"""
        Statement:
        ---
        {{statement}}
        ---
        Judge.
        """
    );

    public static (string System, string User) AmendmentSubstantive(string prior, string amended) =>
    (
        $$"""
        You judge whether an amendment SUBSTANTIVELY changes a provision (a real carve-out / shifted
        position) versus merely RESTATING it in different words (cosmetic).
        {{Rules}}
        Shape: {"substantive": <bool>, "reason": "<short>"}
        """,
        $$"""
        Prior version:
        ---
        {{prior}}
        ---
        Amended version:
        ---
        {{amended}}
        ---
        Judge.
        """
    );

    public static (string System, string User) Teeth(string plank) =>
    (
        $$"""
        You judge whether a coalition plank has TEETH: concrete enough to constrain a real
        institution's behavior, not a vague platitude.
        {{Rules}}
        Shape: {"hasTeeth": <bool>, "reason": "<short>"}
        """,
        $$"""
        Plank:
        ---
        {{plank}}
        ---
        Judge.
        """
    );

    public static (string System, string User) Steelman(string provision, string steelman) =>
    (
        $$"""
        You judge a STEELMAN: would a proponent of the provision endorse this as a fair, strong
        statement of their case (not a strawman)? quality 0-100.
        {{Rules}}
        Shape: {"proponentWouldEndorse": <bool>, "quality": <int>, "reason": "<short>"}
        """,
        $$"""
        Provision:
        ---
        {{provision}}
        ---
        Steelman offered by someone who disagrees:
        ---
        {{steelman}}
        ---
        Judge.
        """
    );
}
