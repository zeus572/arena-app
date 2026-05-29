using Civic.API.Models;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Tone and intensity instruction text injected into the generation prompt,
/// plus human-readable labels for the UI. Register, not stereotype: these
/// describe a delivery register, never caricature real political speech.
/// </summary>
public static class CampaignToneGuide
{
    public static string Label(CampaignTone tone) => tone switch
    {
        CampaignTone.Stern => "Stern",
        CampaignTone.Angry => "Defiant",
        CampaignTone.Casual => "Casual",
        CampaignTone.Hopeful => "Hopeful",
        CampaignTone.Sarcastic => "Dry",
        CampaignTone.Presidential => "Statesmanlike",
        CampaignTone.Folksy => "Folksy",
        CampaignTone.Wonkish => "Wonkish",
        _ => tone.ToString(),
    };

    public static string IntensityLabel(int intensity) => intensity switch
    {
        1 => "Measured",
        2 => "Concerned",
        3 => "Engaged",
        4 => "Heated",
        5 => "Fired up",
        _ => "Engaged",
    };

    public static string ToneInstruction(CampaignTone tone) => tone switch
    {
        CampaignTone.Stern =>
            "Authoritative and serious. Short declarative sentences. No jokes, no hedging. Name the stakes plainly.",
        CampaignTone.Angry =>
            "Controlled outrage. Name the wrong directly. No hedging language. One exclamation point maximum. Never attack a person, only the policy or outcome.",
        CampaignTone.Casual =>
            "Conversational and plain. 'Here's the deal' register. Contractions are fine. Talk to the reader, not at them.",
        CampaignTone.Hopeful =>
            "Forward-looking and warm. First-person plural ('we', 'us'). Name a concrete better outcome. End on possibility, not fear.",
        CampaignTone.Sarcastic =>
            "Dry and ironic. Reference the opposing position as if it were plainly unworkable, without name-calling. Use 'apparently' or 'remarkable' at most once. End on the unspoken implication.",
        CampaignTone.Presidential =>
            "Measured, with gravitas. Calm cadence. Speak to shared duty and the long view. Avoid slogans.",
        CampaignTone.Folksy =>
            "Down-to-earth and anecdotal. One concrete image — a kitchen table, a small town, a family member. Avoid policy jargon. End on a question or invitation.",
        CampaignTone.Wonkish =>
            "Data-forward. Lead with the specific mechanism or figure. One technical term, defined inline. Acknowledge the tradeoff explicitly.",
        _ => "Clear, balanced, and plain-spoken.",
    };

    public static string IntensityInstruction(int intensity) => intensity switch
    {
        1 => "Keep it calm and measured. No urgency markers.",
        2 => "Engaged but composed. Mild concern is fine.",
        3 => "Energetic and direct. Make the point land.",
        4 => "Heated. Strong verbs, clear stakes. Stay sharp, never cruel.",
        5 => "Fired up. Maximum conviction — but sharp, not inflammatory. No slurs, no threats, no targeting of real individuals.",
        _ => "Engaged but composed.",
    };
}
