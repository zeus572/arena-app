namespace Civic.API.Services.TaxModel;

/// <summary>
/// Real per-state parameters (§4.3), verified 2025-2026. v1 ships 8 representative
/// states spanning the full structural range (progressive / flat / none); the engine
/// already supports all 50 — the remaining 42 are data work, deferred (§8).
///
/// Single-filer income schedules are simplified to top-line brackets; sales rates are
/// combined state+local averages; property rates are state effective averages;
/// consumptionShare = 0.40 across all states in v1 (flagged as an assumption in the UI).
/// The notes prose is stored verbatim, never generated at compute time.
/// </summary>
public static class StateProfiles
{
    public const double ConsumptionShare = 0.40; // §4: taxable consumption as a fraction of income (v1 assumption)

    public static readonly IReadOnlyList<StateProfile> All = new[]
    {
        new StateProfile(
            Code: "CA",
            Name: "California",
            Glyph: "🌅",
            IncomeSummary: "Progressive, 1%–13.3% (incl. 1% MHS surcharge >$1M)",
            // State std. deduction ≈ $5,540. Top 13.3% = 12.3% + 1% Mental Health
            // Services surcharge on taxable income over $1M.
            Income: IncomeRule.Progressive(5_540,
                new TaxBracket(0, 0.01),
                new TaxBracket(10_756, 0.02),
                new TaxBracket(25_499, 0.04),
                new TaxBracket(40_245, 0.06),
                new TaxBracket(55_866, 0.08),
                new TaxBracket(70_606, 0.093),
                new TaxBracket(360_659, 0.103),
                new TaxBracket(432_787, 0.113),
                new TaxBracket(721_314, 0.123),
                new TaxBracket(1_000_000, 0.133)),
            SalesRate: 0.0882,
            ConsumptionShare: ConsumptionShare,
            PropRate: 0.0068,
            HomeMultiple: 4.5,
            Notes: "Top 13.3% rate includes a 1% Mental Health Services surcharge over $1M. " +
                   "No tax on Social Security benefits. Prop. 13 caps assessed-value growth, " +
                   "so effective rates on long-held homes run lower."),

        new StateProfile(
            Code: "NY",
            Name: "New York",
            Glyph: "🗽",
            IncomeSummary: "Progressive, 4%–10.9%",
            Income: IncomeRule.Progressive(8_000,
                new TaxBracket(0, 0.04),
                new TaxBracket(8_500, 0.045),
                new TaxBracket(11_700, 0.0525),
                new TaxBracket(80_650, 0.055),
                new TaxBracket(215_400, 0.06),
                new TaxBracket(1_077_550, 0.0685),
                new TaxBracket(5_000_000, 0.0965),
                new TaxBracket(25_000_000, 0.109)),
            SalesRate: 0.0853,
            ConsumptionShare: ConsumptionShare,
            PropRate: 0.0140,
            HomeMultiple: 3.5,
            Notes: "NYC and Yonkers stack a local income tax on top of the state rate — " +
                   "not modeled here, so a city resident's burden is higher. " +
                   "High property tax, especially outside NYC."),

        new StateProfile(
            Code: "TX",
            Name: "Texas",
            Glyph: "🤠",
            IncomeSummary: "None",
            Income: IncomeRule.None(),
            SalesRate: 0.0820,
            ConsumptionShare: ConsumptionShare,
            PropRate: 0.0158,
            HomeMultiple: 3.2,
            Notes: "No personal income tax. The state leans on some of the nation's highest " +
                   "property taxes and a high combined sales tax to fund itself."),

        new StateProfile(
            Code: "FL",
            Name: "Florida",
            Glyph: "🌴",
            IncomeSummary: "None",
            Income: IncomeRule.None(),
            SalesRate: 0.0700,
            ConsumptionShare: ConsumptionShare,
            PropRate: 0.0091,
            HomeMultiple: 3.6,
            Notes: "No personal income tax. Homestead exemption and a 'Save Our Homes' assessment " +
                   "cap soften property tax for primary residents. Tourism shifts some sales-tax " +
                   "burden onto visitors."),

        new StateProfile(
            Code: "WA",
            Name: "Washington",
            Glyph: "🌲",
            IncomeSummary: "None (7% cap-gains tax >$262k only)",
            Income: IncomeRule.None(),
            SalesRate: 0.0938,
            ConsumptionShare: ConsumptionShare,
            PropRate: 0.0087,
            HomeMultiple: 4.2,
            Notes: "No wage income tax, but a 7% capital-gains tax applies above ~$262k of gains " +
                   "(not modeled — this calculator assumes wage income). Among the highest combined " +
                   "sales-tax rates in the country."),

        new StateProfile(
            Code: "CO",
            Name: "Colorado",
            Glyph: "🏔️",
            IncomeSummary: "Flat 4.40%",
            // Flat 4.4% on federal taxable income — std. deduction mirrors federal $15,750.
            Income: IncomeRule.Flat(0.044, 15_750),
            SalesRate: 0.0781,
            ConsumptionShare: ConsumptionShare,
            PropRate: 0.0049,
            HomeMultiple: 4.8,
            Notes: "Flat 4.4% on federal taxable income. Low effective property tax rate, " +
                   "but high home values raise the dollar amount."),

        new StateProfile(
            Code: "PA",
            Name: "Pennsylvania",
            Glyph: "🔔",
            IncomeSummary: "Flat 3.07%, no std. deduction",
            Income: IncomeRule.Flat(0.0307, 0),
            SalesRate: 0.0634,
            ConsumptionShare: ConsumptionShare,
            PropRate: 0.0149,
            HomeMultiple: 2.9,
            Notes: "Flat 3.07% with no standard deduction — the first dollar is taxed. " +
                   "Many municipalities add a local earned-income tax (~1%), not modeled here."),

        new StateProfile(
            Code: "IL",
            Name: "Illinois",
            Glyph: "🌽",
            IncomeSummary: "Flat 4.95%, no std. deduction",
            Income: IncomeRule.Flat(0.0495, 0),
            SalesRate: 0.0886,
            ConsumptionShare: ConsumptionShare,
            PropRate: 0.0188,
            HomeMultiple: 3.0,
            Notes: "Flat 4.95%, no standard deduction (a small personal exemption applies instead, " +
                   "omitted here). Among the highest effective property tax rates in the nation. " +
                   "Retirement income is exempt."),
    };

    private static readonly Dictionary<string, StateProfile> ByCode =
        All.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>Look up a state profile by 2-letter code (case-insensitive). Null if unknown.</summary>
    public static StateProfile? Find(string? code) =>
        code is not null && ByCode.TryGetValue(code, out var p) ? p : null;
}
