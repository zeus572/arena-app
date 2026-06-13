namespace Civic.API.Models.DTOs;

// Wire shapes for the Tax Apportionment module (§5). Stateless, cacheable, LLM-free.

public class TaxBracketDto
{
    public double Lower { get; set; }
    public double Rate { get; set; }
}

/// <summary>
/// A state's income-tax rule, serialized to match the TypeScript engine's IncomeRule
/// union: <c>type</c> is "none" | "flat" | "progressive"; the irrelevant fields are null.
/// </summary>
public class TaxIncomeRuleDto
{
    public string Type { get; set; } = "none";
    public double? Rate { get; set; }
    public double? StdDed { get; set; }
    public List<TaxBracketDto>? Brackets { get; set; }
}

/// <summary>
/// Full per-state profile: GET /api/tax-model/states. Shaped to match the frontend
/// StateProfile type so the client engine computes directly from it (one source of truth).
/// </summary>
public class TaxStateSummaryDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Glyph { get; set; } = "";
    public string IncomeSummary { get; set; } = "";
    public TaxIncomeRuleDto Income { get; set; } = new();
    public double SalesRate { get; set; }
    public double ConsumptionShare { get; set; }
    public double PropRate { get; set; }
    public double HomeMultiple { get; set; }
    public string Notes { get; set; } = "";
}

public class FederalBreakdownDto
{
    public double IncomeTax { get; set; }
    public double SocialSecurity { get; set; }
    public double Medicare { get; set; }
    public double AddlMedicare { get; set; }
    public double Fica { get; set; }
    public double Total { get; set; }
    public double EffectiveRate { get; set; }
}

public class StateBreakdownDto
{
    public double IncomeTax { get; set; }
    public double SalesTax { get; set; }
    public double PropertyTax { get; set; }
    public double Total { get; set; }
    public double EffectiveRate { get; set; }
}

public class CombinedBreakdownDto
{
    public double Total { get; set; }
    public double EffectiveRate { get; set; }
    public double FederalShare { get; set; }
    public double StateShare { get; set; }
}

/// <summary>Full compute result: GET /api/tax-model/compute.</summary>
public class TaxComputeDto
{
    public double Income { get; set; }
    public string Filing { get; set; } = "";
    public string State { get; set; } = "";
    public FederalBreakdownDto Federal { get; set; } = new();
    public StateBreakdownDto StateLocal { get; set; } = new();
    public CombinedBreakdownDto Combined { get; set; } = new();
}

/// <summary>One row of the scaling table (§7.4): a preset income + its split.</summary>
public class TaxLadderRowDto
{
    public double Income { get; set; }
    public double FederalTotal { get; set; }
    public double StateTotal { get; set; }
    public double CombinedTotal { get; set; }
    public double FederalShare { get; set; }
    public double StateShare { get; set; }
    public double EffectiveRate { get; set; }
}

/// <summary>The scaling table for one state/filing: GET /api/tax-model/ladder.</summary>
public class TaxLadderDto
{
    public string Filing { get; set; } = "";
    public string State { get; set; } = "";
    public List<TaxLadderRowDto> Rows { get; set; } = new();
}
