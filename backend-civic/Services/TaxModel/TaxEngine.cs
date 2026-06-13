namespace Civic.API.Services.TaxModel;

/// <summary>
/// Deterministic, closed-form tax engine (§3, §4). Pure static functions — no
/// state, no I/O, no LLM. This C# engine and the TypeScript engine shipped to
/// the browser (frontend-civic/src/taxModel/engine) are kept in lockstep: both
/// reproduce the same golden-value table (§6). A literal shared module across
/// the C#/TS boundary isn't possible, so the contract is enforced by the
/// identical golden tests in each project.
/// </summary>
public static class TaxEngine
{
    /// <summary>
    /// Walk a marginal bracket schedule. Each band's rate applies only to the
    /// taxable income that falls within it. Brackets must be ordered ascending
    /// by lower bound.
    /// </summary>
    public static double Progressive(double taxableIncome, IReadOnlyList<TaxBracket> brackets)
    {
        if (taxableIncome <= 0 || brackets.Count == 0) return 0;

        double tax = 0;
        for (int i = 0; i < brackets.Count; i++)
        {
            double lower = brackets[i].Lower;
            if (taxableIncome <= lower) break;

            double upper = i + 1 < brackets.Count ? brackets[i + 1].Lower : double.PositiveInfinity;
            double bandTop = Math.Min(taxableIncome, upper);
            tax += (bandTop - lower) * brackets[i].Rate;
        }
        return tax;
    }

    /// <summary>Federal tax for one gross wage income (§3.4).</summary>
    public static FederalResult ComputeFederal(double income, FilingStatus filing)
    {
        if (income < 0) income = 0;

        double taxableIncome = Math.Max(0, income - TaxConstants.StandardDeduction(filing));
        double incomeTax = Progressive(taxableIncome, TaxConstants.Brackets(filing));

        double socialSecurity = Math.Min(income, TaxConstants.SocialSecurityWageBase) * TaxConstants.SocialSecurityRate;
        double medicare = income * TaxConstants.MedicareRate;
        double addlMedicare = Math.Max(0, income - TaxConstants.AdditionalMedicareThreshold(filing)) * TaxConstants.AdditionalMedicareRate;
        double fica = socialSecurity + medicare + addlMedicare;

        double total = incomeTax + fica;
        double effectiveRate = income > 0 ? total / income : 0;

        return new FederalResult(incomeTax, socialSecurity, medicare, addlMedicare, fica, total, effectiveRate);
    }

    /// <summary>State income tax under a state's <see cref="IncomeRule"/> (§4.2).</summary>
    public static double ComputeStateIncomeTax(double income, IncomeRule rule)
    {
        if (income < 0) income = 0;

        return rule.Kind switch
        {
            IncomeRuleKind.None => 0,
            IncomeRuleKind.Flat => Math.Max(0, income - rule.StdDed) * rule.Rate,
            IncomeRuleKind.Progressive => Progressive(
                Math.Max(0, income - rule.StdDed),
                rule.Brackets ?? Array.Empty<TaxBracket>()),
            _ => 0,
        };
    }

    /// <summary>State + local tax for one income against a state profile (§4.2).</summary>
    public static StateResult ComputeState(double income, StateProfile profile)
    {
        if (income < 0) income = 0;

        double incomeTax = ComputeStateIncomeTax(income, profile.Income);
        double salesTax = income * profile.ConsumptionShare * profile.SalesRate;
        double propertyTax = income * profile.HomeMultiple * profile.PropRate;
        double total = incomeTax + salesTax + propertyTax;
        double effectiveRate = income > 0 ? total / income : 0;

        return new StateResult(incomeTax, salesTax, propertyTax, total, effectiveRate);
    }

    /// <summary>
    /// Combine federal and state/local into the apportionment split. When total
    /// is zero (income 0), shares default to a neutral 0 so the bar renders empty
    /// rather than NaN.
    /// </summary>
    public static CombinedResult Combine(double income, FederalResult federal, StateResult state)
    {
        double total = federal.Total + state.Total;
        double effectiveRate = income > 0 ? total / income : 0;
        double federalShare = total > 0 ? federal.Total / total : 0;
        double stateShare = total > 0 ? state.Total / total : 0;
        return new CombinedResult(total, effectiveRate, federalShare, stateShare);
    }
}
