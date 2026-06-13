namespace Civic.API.Services.TaxModel;

/// <summary>
/// Federal tax constants — source of truth (§3). All figures verified 2025-2026.
/// The annual refresh is a contained edit: bump <see cref="TaxYear"/>, the wage
/// base, the brackets, and the standard deductions.
/// </summary>
public static class TaxConstants
{
    // verified 2025-2026
    public const int TaxYear = 2025;

    // --- FICA / payroll (§3.3) ---

    /// <summary>Social Security (OASDI) employee rate.</summary>
    public const double SocialSecurityRate = 0.062;

    /// <summary>2025 SS wage base. 2026 rises to 184,500 — one-line update.</summary>
    public const double SocialSecurityWageBase = 176_100;

    /// <summary>Medicare (HI) employee rate — no cap.</summary>
    public const double MedicareRate = 0.0145;

    /// <summary>Additional Medicare rate on wages over the filing threshold (employee-only).</summary>
    public const double AdditionalMedicareRate = 0.009;

    /// <summary>Additional Medicare wage thresholds by filing status (§3.3).</summary>
    public static double AdditionalMedicareThreshold(FilingStatus filing) => filing switch
    {
        FilingStatus.Single => 200_000,
        FilingStatus.MarriedFilingJointly => 250_000,
        _ => 200_000,
    };

    // --- Standard deduction (§3.2, 2025 OBBBA) ---

    public static double StandardDeduction(FilingStatus filing) => filing switch
    {
        FilingStatus.Single => 15_750,
        FilingStatus.MarriedFilingJointly => 31_500,
        _ => 15_750,
    };

    // --- Ordinary income brackets (§3.1, IRS Rev. Proc. 2024-40) ---
    // Marginal: each rate applies only to income within its band. Lower bound → rate.

    private static readonly TaxBracket[] SingleBrackets =
    {
        new(0, 0.10),
        new(11_925, 0.12),
        new(48_475, 0.22),
        new(103_350, 0.24),
        new(197_300, 0.32),
        new(250_525, 0.35),
        new(626_350, 0.37),
    };

    private static readonly TaxBracket[] MarriedFilingJointlyBrackets =
    {
        new(0, 0.10),
        new(23_850, 0.12),
        new(96_950, 0.22),
        new(206_700, 0.24),
        new(394_600, 0.32),
        new(501_050, 0.35),
        new(751_600, 0.37),
    };

    public static IReadOnlyList<TaxBracket> Brackets(FilingStatus filing) => filing switch
    {
        FilingStatus.Single => SingleBrackets,
        FilingStatus.MarriedFilingJointly => MarriedFilingJointlyBrackets,
        _ => SingleBrackets,
    };
}
