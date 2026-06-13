namespace Civic.API.Services.TaxModel;

// Tax Apportionment engine — deterministic, LLM-free. See docs/TaxApportionment_Spec.md.
// All compute is closed-form; no model calls. Federal law constants live in
// TaxConstants; per-state parameters live in StateProfiles.

/// <summary>Federal filing status. Only the two v1 statuses are modeled (§3).</summary>
public enum FilingStatus
{
    Single,
    MarriedFilingJointly,
}

/// <summary>Discriminator for a state's income-tax structure (§4.1).</summary>
public enum IncomeRuleKind
{
    None,
    Flat,
    Progressive,
}

/// <summary>
/// A single income-tax bracket: <paramref name="Lower"/> is the lower bound of
/// taxable income at which <paramref name="Rate"/> begins to apply (marginal).
/// </summary>
public readonly record struct TaxBracket(double Lower, double Rate);

/// <summary>
/// A state's income-tax rule (§4.1). Build via the factory helpers so the
/// inactive fields stay zero/empty:
/// <list type="bullet">
///   <item><see cref="None"/> — no wage income tax (TX/FL/WA).</item>
///   <item><see cref="Flat"/> — single rate on (income − stdDed).</item>
///   <item><see cref="Progressive"/> — marginal bands on (income − stdDed).</item>
/// </list>
/// </summary>
public sealed record IncomeRule(
    IncomeRuleKind Kind,
    double Rate = 0,
    double StdDed = 0,
    IReadOnlyList<TaxBracket>? Brackets = null)
{
    public static IncomeRule None() => new(IncomeRuleKind.None);

    public static IncomeRule Flat(double rate, double stdDed) =>
        new(IncomeRuleKind.Flat, Rate: rate, StdDed: stdDed);

    public static IncomeRule Progressive(double stdDed, params TaxBracket[] brackets) =>
        new(IncomeRuleKind.Progressive, StdDed: stdDed, Brackets: brackets);
}

/// <summary>
/// Real per-state parameters (§4). Single-filer income schedule; sales/property
/// rates are combined state+local / effective-average figures. <see cref="Notes"/>
/// is stored prose (drafted offline), never generated at compute time.
/// </summary>
public sealed record StateProfile(
    string Code,
    string Name,
    string Glyph,
    string IncomeSummary,
    IncomeRule Income,
    double SalesRate,
    double ConsumptionShare,
    double PropRate,
    double HomeMultiple,
    string Notes);

/// <summary>Federal tax breakdown for one income (§3.4). Dollars, unrounded.</summary>
public readonly record struct FederalResult(
    double IncomeTax,
    double SocialSecurity,
    double Medicare,
    double AddlMedicare,
    double Fica,
    double Total,
    double EffectiveRate);

/// <summary>State + local tax breakdown for one income (§4.2). Dollars, unrounded.</summary>
public readonly record struct StateResult(
    double IncomeTax,
    double SalesTax,
    double PropertyTax,
    double Total,
    double EffectiveRate);

/// <summary>Combined federal + state/local view, including the apportionment split.</summary>
public readonly record struct CombinedResult(
    double Total,
    double EffectiveRate,
    double FederalShare,
    double StateShare);
