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

public interface ICivicCatalog
{
    IReadOnlyList<AxisDefinition> Axes { get; }
    IReadOnlyList<ArchetypeDefinition> Archetypes { get; }
    IReadOnlyList<BudgetCategoryDefinition> BudgetCategories { get; }
    AxisDefinition? AxisFor(string key);
    ArchetypeDefinition? ArchetypeFor(string key);
    BudgetCategoryDefinition? BudgetCategoryFor(string key);
}

public class CivicCatalog : ICivicCatalog
{
    public IReadOnlyList<AxisDefinition> Axes { get; }
    public IReadOnlyList<ArchetypeDefinition> Archetypes { get; }
    public IReadOnlyList<BudgetCategoryDefinition> BudgetCategories { get; }

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

        _axisByKey = Axes.ToDictionary(a => a.Key);
        _archetypeByKey = Archetypes.ToDictionary(a => a.Key);
        _budgetByKey = BudgetCategories.ToDictionary(b => b.Key);
    }

    public AxisDefinition? AxisFor(string key) =>
        _axisByKey.TryGetValue(key, out var a) ? a : null;

    public ArchetypeDefinition? ArchetypeFor(string key) =>
        _archetypeByKey.TryGetValue(key, out var a) ? a : null;

    public BudgetCategoryDefinition? BudgetCategoryFor(string key) =>
        _budgetByKey.TryGetValue(key, out var b) ? b : null;
}
