using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Civic.API.Mapping;
using Civic.API.Models.DTOs;
using Civic.API.Services.TaxModel;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Tax Apportionment model (§5). Stateless, cacheable, LLM-free — every response
/// is a pure function of the query and the shipped constants. The same compute
/// also runs client-side from the TypeScript engine; this endpoint exists for
/// server-rendered briefings and as the one canonical server implementation.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/tax-model")]
public class TaxModelController : ControllerBase
{
    // Preset incomes for the scaling table (§5 / §7.4).
    private static readonly double[] LadderIncomes = { 30_000, 60_000, 100_000, 175_000, 350_000, 750_000 };

    // GET /api/tax-model/states — for state pickers + cards.
    [HttpGet("states")]
    public ActionResult<IEnumerable<TaxStateSummaryDto>> States() =>
        Ok(StateProfiles.All.Select(s => s.ToSummaryDto()));

    // GET /api/tax-model/compute?income=100000&filing=single&state=CA
    [HttpGet("compute")]
    public ActionResult<TaxComputeDto> Compute(
        [FromQuery] double income,
        [FromQuery] string filing = "single",
        [FromQuery] string state = "CA")
    {
        if (income < 0)
            return ValidationProblem("income must be non-negative.");
        if (!TryParseFiling(filing, out var filingStatus, out var filingKey))
            return ValidationProblem($"Unknown filing status '{filing}'. Use 'single' or 'mfj'.");

        var profile = StateProfiles.Find(state);
        if (profile is null)
            return ValidationProblem($"Unknown state '{state}'. v1 ships: {string.Join(", ", StateProfiles.All.Select(s => s.Code))}.");

        return Ok(BuildCompute(income, filingStatus, filingKey, profile));
    }

    // GET /api/tax-model/ladder?filing=single&state=CA
    [HttpGet("ladder")]
    public ActionResult<TaxLadderDto> Ladder(
        [FromQuery] string filing = "single",
        [FromQuery] string state = "CA")
    {
        if (!TryParseFiling(filing, out var filingStatus, out var filingKey))
            return ValidationProblem($"Unknown filing status '{filing}'. Use 'single' or 'mfj'.");

        var profile = StateProfiles.Find(state);
        if (profile is null)
            return ValidationProblem($"Unknown state '{state}'. v1 ships: {string.Join(", ", StateProfiles.All.Select(s => s.Code))}.");

        var rows = LadderIncomes.Select(income =>
        {
            var federal = TaxEngine.ComputeFederal(income, filingStatus);
            var stateLocal = TaxEngine.ComputeState(income, profile);
            var combined = TaxEngine.Combine(income, federal, stateLocal);
            return new TaxLadderRowDto
            {
                Income = income,
                FederalTotal = federal.Total,
                StateTotal = stateLocal.Total,
                CombinedTotal = combined.Total,
                FederalShare = combined.FederalShare,
                StateShare = combined.StateShare,
                EffectiveRate = combined.EffectiveRate,
            };
        }).ToList();

        return Ok(new TaxLadderDto { Filing = filingKey, State = profile.Code, Rows = rows });
    }

    private static TaxComputeDto BuildCompute(
        double income, FilingStatus filingStatus, string filingKey, StateProfile profile)
    {
        var federal = TaxEngine.ComputeFederal(income, filingStatus);
        var stateLocal = TaxEngine.ComputeState(income, profile);
        var combined = TaxEngine.Combine(income, federal, stateLocal);

        return new TaxComputeDto
        {
            Income = income,
            Filing = filingKey,
            State = profile.Code,
            Federal = federal.ToDto(),
            StateLocal = stateLocal.ToDto(),
            Combined = combined.ToDto(),
        };
    }

    private static bool TryParseFiling(string? filing, out FilingStatus status, out string key)
    {
        switch (filing?.Trim().ToLowerInvariant())
        {
            case "single" or "s":
                status = FilingStatus.Single;
                key = "single";
                return true;
            case "mfj" or "married" or "marriedfilingjointly" or "joint":
                status = FilingStatus.MarriedFilingJointly;
                key = "mfj";
                return true;
            default:
                status = FilingStatus.Single;
                key = "single";
                return false;
        }
    }
}
