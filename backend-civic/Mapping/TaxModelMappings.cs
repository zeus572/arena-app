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
        SalesRate = p.SalesRate,
        PropRate = p.PropRate,
        Notes = p.Notes,
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
