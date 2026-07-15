namespace Civic.API.Mapping;

/// <summary>Small presentation helpers for bills, shared by the controller and tests.</summary>
public static class BillMappings
{
    /// <summary>Human display id, e.g. "HR 1 · 118th Congress" (federal) or "HR 1" otherwise.</summary>
    public static string Identifier(string billType, int number, int congress)
    {
        var baseId = $"{billType} {number}";
        return congress > 0 ? $"{baseId} · {Ordinal(congress)} Congress" : baseId;
    }

    /// <summary>Prefer the neutral LLM synthesis summary; fall back to the source summary.</summary>
    public static string Teaser(string? synthesisSummary, string sourceSummary)
    {
        var text = !string.IsNullOrWhiteSpace(synthesisSummary) ? synthesisSummary! : sourceSummary;
        text = text.Trim();
        const int max = 240;
        return text.Length <= max ? text : text.Substring(0, max).TrimEnd() + "…";
    }

    private static string Ordinal(int n)
    {
        int mod100 = n % 100;
        string suffix = (mod100 is >= 11 and <= 13)
            ? "th"
            : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }
}
