using Civic.API.Models.DTOs;
using Civic.API.Services.TaxModel;

namespace Civic.API.Mapping;

/// <summary>Maps tax-engine results (§3/§4) to the wire DTOs (§5).</summary>
public static class TaxModelMappings
{
    public static TaxStateSummaryDto ToSummaryDto(this StateProfile p) => new()
    {
        Code = p.Code,
        Name = p.Name,
        Glyph = p.Glyph,
        IncomeSummary = p.IncomeSummary,
        Income = p.Income.ToDto(),
        SalesRate = p.SalesRate,
        ConsumptionShare = p.ConsumptionShare,
        PropRate = p.PropRate,
        HomeMultiple = p.HomeMultiple,
        Notes = p.Notes,
    };

    public static TaxIncomeRuleDto ToDto(this IncomeRule rule) => rule.Kind switch
    {
        IncomeRuleKind.None => new TaxIncomeRuleDto { Type = "none" },
        IncomeRuleKind.Flat => new TaxIncomeRuleDto { Type = "flat", Rate = rule.Rate, StdDed = rule.StdDed },
        IncomeRuleKind.Progressive => new TaxIncomeRuleDto
        {
            Type = "progressive",
            StdDed = rule.StdDed,
            Brackets = (rule.Brackets ?? Array.Empty<TaxBracket>())
                .Select(b => new TaxBracketDto { Lower = b.Lower, Rate = b.Rate }).ToList(),
        },
        _ => new TaxIncomeRuleDto { Type = "none" },
    };

    public static FederalBreakdownDto ToDto(this FederalResult r) => new()
    {
        IncomeTax = r.IncomeTax,
        SocialSecurity = r.SocialSecurity,
        Medicare = r.Medicare,
        AddlMedicare = r.AddlMedicare,
        Fica = r.Fica,
        Total = r.Total,
        EffectiveRate = r.EffectiveRate,
    };

    public static StateBreakdownDto ToDto(this StateResult r) => new()
    {
        IncomeTax = r.IncomeTax,
        SalesTax = r.SalesTax,
        PropertyTax = r.PropertyTax,
        Total = r.Total,
        EffectiveRate = r.EffectiveRate,
    };

    public static CombinedBreakdownDto ToDto(this CombinedResult r) => new()
    {
        Total = r.Total,
        EffectiveRate = r.EffectiveRate,
        FederalShare = r.FederalShare,
        StateShare = r.StateShare,
    };
}
