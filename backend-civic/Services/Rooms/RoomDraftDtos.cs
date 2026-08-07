namespace Civic.API.Services.Rooms;

/// <summary>
/// What the model is asked to return for one Story Room draft.
///
/// Every field maps to something the Story Room page already renders. Nothing here is free
/// text the page will not show — a prompt that invites prose nobody displays produces prose
/// nobody checks.
/// </summary>
public class RoomDraftResult
{
    public string Title { get; set; } = "";
    public string Dek { get; set; } = "";
    public string StoryType { get; set; } = "";
    public string HowItWorksIntro { get; set; } = "";
    public List<DraftDimension> WhyItMatters { get; set; } = new();
    public List<DraftStakeholder> Stakeholders { get; set; } = new();
    public List<DraftNextStep> NextSteps { get; set; } = new();
    public List<DraftClaim> Claims { get; set; } = new();

    /// <summary>
    /// Terms from the contested-terms guide the model believes appear in its source.
    ///
    /// Asked for, not trusted: the terminology gate re-checks the drafted text against
    /// <see cref="ICivicCatalog.ContestedTermsIn"/> regardless of what comes back here. The
    /// field exists so a reviewer can see whether the model noticed.
    /// </summary>
    public List<string> ContestedTermsNoticed { get; set; } = new();
}

public class DraftDimension
{
    /// <summary>Legal · Institutional · Financial · Human · Immediate · Longer term.</summary>
    public string Dimension { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>Index into <see cref="RoomDraftResult.Claims"/>, or null.</summary>
    public int? ClaimIndex { get; set; }
}

public class DraftStakeholder
{
    public string Group { get; set; } = "";
    public string ImpactSummary { get; set; } = "";
    public double Confidence { get; set; } = 0.5;
}

public class DraftNextStep
{
    public string Description { get; set; } = "";
    /// <summary>"Confirmed if:" — an objective test, required.</summary>
    public string VerificationCondition { get; set; } = "";
    public string? ExpectedTiming { get; set; }
}

/// <summary>
/// One claim extracted from the source document.
///
/// <see cref="SupportingPassage"/> must be a verbatim span of the source. PRD 04 §7 requires
/// the exact passage, and the extraction service verifies it appears in the text rather than
/// taking the model's word for it — a paraphrase that the model calls a quotation is the
/// single most damaging thing this pipeline could produce.
/// </summary>
public class DraftClaim
{
    public string Text { get; set; } = "";
    /// <summary>Factual | Interpretation | Opinion | Prediction.</summary>
    public string Kind { get; set; } = "Factual";
    /// <summary>One of the eight ClaimStatus members.</summary>
    public string Status { get; set; } = "PlausibleButUnresolved";
    public string EvidenceSummary { get; set; } = "";
    /// <summary>Required. A claim nobody can say how to settle is not a claim.</summary>
    public string WhatWouldSettleIt { get; set; } = "";
    public string SupportingPassage { get; set; } = "";
}
