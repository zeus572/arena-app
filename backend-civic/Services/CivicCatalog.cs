namespace Civic.API.Services;

public class AxisDefinition
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string LowLabel { get; set; } = "";
    public string HighLabel { get; set; } = "";
    public int Order { get; set; }
}

public class ArchetypeDefinition
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ArchetypeAxisExpectation> AxisVector { get; set; } = new();
}

public class ArchetypeAxisExpectation
{
    public string AxisKey { get; set; } = "";
    public double ExpectedScore { get; set; }
}

public class BudgetCategoryDefinition
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Order { get; set; }
    public List<BudgetAxisDelta> AxisDeltas { get; set; } = new();
}

public class BudgetAxisDelta
{
    public string AxisKey { get; set; } = "";
    public double Delta { get; set; }
}

/// <summary>
/// A word whose use implies a judgement or a legal conclusion (PRD 07 §3.5).
///
/// The TerminologyReview publish gate matches room copy against these and requires a
/// terminology note before anything using one can publish. "Neutral wording" is otherwise
/// an instruction nobody can check.
/// </summary>
public class ContestedTermDefinition
{
    public string Term { get; set; } = "";
    public string[] Aliases { get; set; } = Array.Empty<string>();
    /// <summary>What to do instead. Shown to the editor at the point of the block.</summary>
    public string Guidance { get; set; } = "";
    /// <summary>When true, using the term blocks publish until a TerminologyNote exists.</summary>
    public bool RequiresNote { get; set; } = true;
}

public interface ICivicCatalog
{
    IReadOnlyList<AxisDefinition> Axes { get; }
    IReadOnlyList<ArchetypeDefinition> Archetypes { get; }
    IReadOnlyList<BudgetCategoryDefinition> BudgetCategories { get; }
    IReadOnlyList<ContestedTermDefinition> ContestedTerms { get; }
    AxisDefinition? AxisFor(string key);
    ArchetypeDefinition? ArchetypeFor(string key);
    BudgetCategoryDefinition? BudgetCategoryFor(string key);
    /// <summary>Every contested term appearing in the text, matched on whole words.</summary>
    IReadOnlyList<ContestedTermDefinition> ContestedTermsIn(string? text);
}

public class CivicCatalog : ICivicCatalog
{
    public IReadOnlyList<AxisDefinition> Axes { get; }
    public IReadOnlyList<ArchetypeDefinition> Archetypes { get; }
    public IReadOnlyList<BudgetCategoryDefinition> BudgetCategories { get; }
    public IReadOnlyList<ContestedTermDefinition> ContestedTerms { get; }

    private readonly Dictionary<string, AxisDefinition> _axisByKey;
    private readonly Dictionary<string, ArchetypeDefinition> _archetypeByKey;
    private readonly Dictionary<string, BudgetCategoryDefinition> _budgetByKey;

    public CivicCatalog()
    {
        Axes = SeedService.LoadJson<List<AxisDefinition>>("Seed.axes.json")
            ?? throw new InvalidOperationException("Seed/axes.json failed to load.");
        Archetypes = SeedService.LoadJson<List<ArchetypeDefinition>>("Seed.archetypes.json")
            ?? throw new InvalidOperationException("Seed/archetypes.json failed to load.");
        BudgetCategories = SeedService.LoadJson<List<BudgetCategoryDefinition>>("Seed.budget-categories.json")
            ?? throw new InvalidOperationException("Seed/budget-categories.json failed to load.");

        ContestedTerms = SeedService.LoadJson<List<ContestedTermDefinition>>("Seed.contested-terms.json")
            ?? throw new InvalidOperationException("Seed/contested-terms.json failed to load.");

        _axisByKey = Axes.ToDictionary(a => a.Key);
        _archetypeByKey = Archetypes.ToDictionary(a => a.Key);
        _budgetByKey = BudgetCategories.ToDictionary(b => b.Key);
    }

    /// <inheritdoc />
    public IReadOnlyList<ContestedTermDefinition> ContestedTermsIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<ContestedTermDefinition>();

        var hits = new List<ContestedTermDefinition>();
        foreach (var term in ContestedTerms)
        {
            var forms = new[] { term.Term }.Concat(term.Aliases);
            // Whole-word only: "cut" must not fire on "executed", and "war" must not fire
            // on "toward". A gate that cries wolf gets clicked through.
            if (forms.Any(f => ContainsWholeWord(text, f))) hits.Add(term);
        }
        return hits;
    }

    private static bool ContainsWholeWord(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return false;

        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var beforeOk = index == 0 || !char.IsLetterOrDigit(haystack[index - 1]);
            var end = index + needle.Length;
            var afterOk = end >= haystack.Length || !char.IsLetterOrDigit(haystack[end]);
            if (beforeOk && afterOk) return true;
            index = end;
        }
        return false;
    }

    public AxisDefinition? AxisFor(string key) =>
        _axisByKey.TryGetValue(key, out var a) ? a : null;

    public ArchetypeDefinition? ArchetypeFor(string key) =>
        _archetypeByKey.TryGetValue(key, out var a) ? a : null;

    public BudgetCategoryDefinition? BudgetCategoryFor(string key) =>
        _budgetByKey.TryGetValue(key, out var b) ? b : null;
}
