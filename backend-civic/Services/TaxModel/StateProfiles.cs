namespace Civic.API.Services.TaxModel;

/// <summary>
/// Per-state parameters for all 50 states. The original 8 (CA/NY/TX/FL/WA/CO/PA/IL)
/// carry the fully verified 2025-2026 figures and rich notes from the spec (§4.3); the
/// remaining 42 use best-effort 2025 figures (combined avg sales rate, effective avg
/// property rate, an estimated home-value-to-income multiple, and a simplified top-line
/// single-filer income schedule). consumptionShare = 0.40 across all states (v1 assumption).
/// Notes prose is stored verbatim, never generated at compute time.
///
/// This is the single source of truth: the frontend fetches these profiles from
/// /api/tax-model/states and computes client-side from the shipped TypeScript engine.
/// </summary>
public static class StateProfiles
{
    public const double ConsumptionShare = 0.40; // §4: taxable consumption as a fraction of income (v1 assumption)

    private static StateProfile None(
        string code, string name, string glyph, double sales, double prop, double home, string notes) =>
        new(code, name, glyph, "None", IncomeRule.None(), sales, ConsumptionShare, prop, home, notes);

    private static StateProfile Flat(
        string code, string name, string glyph, string summary, double rate, double stdDed,
        double sales, double prop, double home, string notes) =>
        new(code, name, glyph, summary, IncomeRule.Flat(rate, stdDed), sales, ConsumptionShare, prop, home, notes);

    private static StateProfile Prog(
        string code, string name, string glyph, string summary, double stdDed, TaxBracket[] brackets,
        double sales, double prop, double home, string notes) =>
        new(code, name, glyph, summary, IncomeRule.Progressive(stdDed, brackets), sales, ConsumptionShare, prop, home, notes);

    private static TaxBracket B(double lower, double rate) => new(lower, rate);

    public static readonly IReadOnlyList<StateProfile> All = new[]
    {
        // ---- The 8 fully verified states (§4.3) ----
        Prog("CA", "California", "🌅", "Progressive, 1%–13.3% (incl. 1% MHS surcharge >$1M)", 5_540,
            new[] { B(0, 0.01), B(10_756, 0.02), B(25_499, 0.04), B(40_245, 0.06), B(55_866, 0.08),
                    B(70_606, 0.093), B(360_659, 0.103), B(432_787, 0.113), B(721_314, 0.123), B(1_000_000, 0.133) },
            0.0882, 0.0068, 4.5,
            "Top 13.3% rate includes a 1% Mental Health Services surcharge over $1M. No tax on Social " +
            "Security benefits. Prop. 13 caps assessed-value growth, so effective rates on long-held homes run lower."),

        Prog("NY", "New York", "🗽", "Progressive, 4%–10.9%", 8_000,
            new[] { B(0, 0.04), B(8_500, 0.045), B(11_700, 0.0525), B(80_650, 0.055), B(215_400, 0.06),
                    B(1_077_550, 0.0685), B(5_000_000, 0.0965), B(25_000_000, 0.109) },
            0.0853, 0.0140, 3.5,
            "NYC and Yonkers stack a local income tax on top of the state rate — not modeled here, so a " +
            "city resident's burden is higher. High property tax, especially outside NYC."),

        None("TX", "Texas", "🤠", 0.0820, 0.0158, 3.2,
            "No personal income tax. The state leans on some of the nation's highest property taxes and a " +
            "high combined sales tax to fund itself."),

        None("FL", "Florida", "🌴", 0.0700, 0.0091, 3.6,
            "No personal income tax. Homestead exemption and a 'Save Our Homes' assessment cap soften " +
            "property tax for primary residents. Tourism shifts some sales-tax burden onto visitors."),

        None("WA", "Washington", "🌲", 0.0938, 0.0087, 4.2,
            "No wage income tax, but a 7% capital-gains tax applies above ~$262k of gains (not modeled — " +
            "this calculator assumes wage income). Among the highest combined sales-tax rates in the country."),

        Flat("CO", "Colorado", "🏔️", "Flat 4.40%", 0.044, 15_750, 0.0781, 0.0049, 4.8,
            "Flat 4.4% on federal taxable income. Low effective property tax rate, but high home values " +
            "raise the dollar amount."),

        Flat("PA", "Pennsylvania", "🔔", "Flat 3.07%, no std. deduction", 0.0307, 0, 0.0634, 0.0149, 2.9,
            "Flat 3.07% with no standard deduction — the first dollar is taxed. Many municipalities add a " +
            "local earned-income tax (~1%), not modeled here."),

        Flat("IL", "Illinois", "🌽", "Flat 4.95%, no std. deduction", 0.0495, 0, 0.0886, 0.0188, 3.0,
            "Flat 4.95%, no standard deduction (a small personal exemption applies instead, omitted here). " +
            "Among the highest effective property tax rates in the nation. Retirement income is exempt."),

        // ---- The remaining 42 (best-effort 2025 figures) ----
        Prog("AL", "Alabama", "🎺", "Progressive, 2%–5%", 2_500,
            new[] { B(0, 0.02), B(500, 0.04), B(3_000, 0.05) }, 0.0929, 0.0040, 2.7,
            "Low income-tax rates but a high combined sales tax, and groceries are still partly taxed. " +
            "Among the lowest effective property taxes in the country."),

        None("AK", "Alaska", "🐻", 0.0182, 0.0104, 3.2,
            "No state income tax and no statewide sales tax — only local sales taxes. Oil revenue funds much " +
            "of the budget and even pays residents an annual dividend."),

        Flat("AZ", "Arizona", "🌵", "Flat 2.50%", 0.025, 14_600, 0.0838, 0.0063, 4.0,
            "Moved to a low 2.5% flat income tax in 2023. Combined sales taxes run high once local rates are added."),

        Prog("AR", "Arkansas", "💎", "Progressive, 2%–3.9%", 2_340,
            new[] { B(0, 0.02), B(4_400, 0.03), B(8_800, 0.039) }, 0.0945, 0.0062, 2.6,
            "Top rate has been cut repeatedly toward a flatter structure. High combined sales tax."),

        Prog("CT", "Connecticut", "⚓", "Progressive, 2%–6.99%", 0,
            new[] { B(0, 0.02), B(10_000, 0.045), B(50_000, 0.055), B(100_000, 0.06), B(200_000, 0.065),
                    B(250_000, 0.069), B(500_000, 0.0699) }, 0.0635, 0.0179, 3.2,
            "No standard deduction; a personal exemption phases out as income rises. Very high property taxes."),

        Prog("DE", "Delaware", "🏖️", "Progressive, 2.2%–6.6%", 3_250,
            new[] { B(2_000, 0.022), B(5_000, 0.039), B(10_000, 0.048), B(20_000, 0.052), B(25_000, 0.0555), B(60_000, 0.066) },
            0.0000, 0.0058, 3.2,
            "No sales tax at all, offset by a gross-receipts tax on businesses. Modest property taxes."),

        Flat("GA", "Georgia", "🍑", "Flat 5.39%", 0.0539, 12_000, 0.0740, 0.0090, 3.2,
            "Moved to a flat ~5.39% rate, scheduled to fall further. Moderate sales and property taxes."),

        Prog("HI", "Hawaii", "🌺", "Progressive, 1.4%–11%", 2_200,
            new[] { B(0, 0.014), B(2_400, 0.032), B(9_600, 0.055), B(24_000, 0.076), B(48_000, 0.079),
                    B(175_000, 0.0825), B(200_000, 0.09), B(300_000, 0.11) }, 0.0450, 0.0032, 6.0,
            "One of the highest top income-tax rates but the lowest effective property tax. Home prices are " +
            "very high relative to income."),

        Flat("ID", "Idaho", "🥔", "Flat 5.695%", 0.05695, 14_600, 0.0603, 0.0067, 4.4,
            "Replaced its brackets with a single ~5.7% rate. Low-to-moderate sales and property taxes."),

        Flat("IN", "Indiana", "🏁", "Flat 3.00%", 0.030, 0, 0.0700, 0.0084, 2.6,
            "Low flat state rate, but counties add their own local income taxes (not modeled here)."),

        Flat("IA", "Iowa", "🌽", "Flat 3.80%", 0.038, 0, 0.0694, 0.0152, 2.5,
            "Moved to a 3.8% flat tax in 2025. High effective property taxes; retirement income is now exempt."),

        Prog("KS", "Kansas", "🌻", "Progressive, 5.2%–5.58%", 3_605,
            new[] { B(0, 0.052), B(23_000, 0.0558) }, 0.0875, 0.0134, 2.6,
            "Two brackets after recent cuts. High combined sales tax, including on groceries (being phased down)."),

        Flat("KY", "Kentucky", "🐎", "Flat 4.00%", 0.040, 3_270, 0.0600, 0.0085, 2.6,
            "Flat rate trending downward toward elimination as revenue triggers are met. Low sales tax."),

        Flat("LA", "Louisiana", "🎷", "Flat 3.00%", 0.030, 0, 0.0956, 0.0056, 2.6,
            "Adopted a 3% flat income tax in 2025. The nation's highest combined sales-tax rate; low property taxes."),

        Prog("ME", "Maine", "🦞", "Progressive, 5.8%–7.15%", 14_600,
            new[] { B(0, 0.058), B(26_050, 0.0675), B(61_600, 0.0715) }, 0.0550, 0.0124, 3.7,
            "A few wide brackets. Generous standard deduction; moderate-to-high property taxes."),

        Prog("MD", "Maryland", "🦀", "Progressive, 2%–5.75%", 2_550,
            new[] { B(0, 0.02), B(1_000, 0.03), B(2_000, 0.04), B(3_000, 0.0475), B(100_000, 0.05),
                    B(125_000, 0.0525), B(150_000, 0.055), B(250_000, 0.0575) }, 0.0600, 0.0105, 3.6,
            "Counties add a local income tax of roughly 2.25%–3.2% on top (not modeled here)."),

        Prog("MA", "Massachusetts", "🦃", "Flat 5% + 4% millionaire surtax", 0,
            new[] { B(0, 0.05), B(1_000_000, 0.09) }, 0.0625, 0.0114, 4.0,
            "Flat 5% with a 4% surtax on income over $1M (the 'Fair Share' amendment). High home values."),

        Flat("MI", "Michigan", "🚗", "Flat 4.25%", 0.0425, 0, 0.0600, 0.0138, 2.6,
            "Flat state rate; many cities levy a separate local income tax (not modeled here)."),

        Prog("MN", "Minnesota", "🛶", "Progressive, 5.35%–9.85%", 14_575,
            new[] { B(0, 0.0535), B(31_690, 0.068), B(104_090, 0.0785), B(193_240, 0.0985) }, 0.0804, 0.0111, 3.2,
            "High top rate and broad-based sales tax (clothing is exempt). Moderate property taxes."),

        Flat("MS", "Mississippi", "🎣", "Flat 4.4% (first $10k exempt)", 0.044, 10_000, 0.0707, 0.0079, 2.6,
            "Flat rate above a $10,000 exemption, scheduled to keep falling. Low property taxes."),

        Prog("MO", "Missouri", "🎻", "Progressive, 2%–4.7%", 14_600,
            new[] { B(0, 0.02), B(2_546, 0.03), B(5_092, 0.04), B(8_419, 0.047) }, 0.0839, 0.0097, 2.7,
            "Top rate is falling toward a flatter structure. St. Louis and Kansas City add a 1% earnings tax (not modeled)."),

        Prog("MT", "Montana", "🏔️", "Progressive, 4.7%–5.9%", 14_600,
            new[] { B(0, 0.047), B(20_500, 0.059) }, 0.0000, 0.0074, 4.3,
            "No general sales tax. Simplified to two income brackets in a recent reform."),

        Prog("NE", "Nebraska", "🌾", "Progressive, 2.46%–5.84%", 7_900,
            new[] { B(0, 0.0246), B(3_700, 0.0351), B(22_170, 0.0501), B(35_730, 0.0584) }, 0.0697, 0.0163, 2.7,
            "Top rate is being cut toward ~3.99%. High effective property taxes fund local schools."),

        None("NV", "Nevada", "🎰", 0.0824, 0.0055, 4.0,
            "No personal income tax; the state leans on sales tax and gaming revenue. Low property taxes."),

        None("NH", "New Hampshire", "🍂", 0.0000, 0.0193, 3.6,
            "No tax on wages and no sales tax (the old interest-and-dividends tax was repealed). " +
            "Among the highest property taxes in the nation."),

        Prog("NJ", "New Jersey", "🏖️", "Progressive, 1.4%–10.75%", 0,
            new[] { B(0, 0.014), B(20_000, 0.0175), B(35_000, 0.035), B(40_000, 0.05525), B(75_000, 0.0637),
                    B(500_000, 0.0897), B(1_000_000, 0.1075) }, 0.0660, 0.0223, 3.8,
            "The highest effective property taxes in the country, paired with a steeply progressive income tax."),

        Prog("NM", "New Mexico", "🌶️", "Progressive, 1.7%–5.9%", 14_600,
            new[] { B(0, 0.017), B(5_500, 0.032), B(11_000, 0.047), B(16_000, 0.049), B(210_000, 0.059) },
            0.0762, 0.0073, 3.6,
            "A gross-receipts tax stands in for a conventional sales tax. Low property taxes."),

        Prog("NC", "North Carolina", "🌲", "Flat 4.25%", 12_750,
            new[] { B(0, 0.0425) }, 0.0700, 0.0080, 3.4,
            "Flat rate trending down toward ~3.99%. Moderate sales and property taxes."),

        Prog("ND", "North Dakota", "🦬", "Progressive, ~2%", 14_600,
            new[] { B(0, 0.0), B(44_725, 0.0195), B(225_975, 0.025) }, 0.0704, 0.0098, 2.7,
            "Among the lowest income-tax rates in the country, cushioned by energy revenue."),

        Prog("OH", "Ohio", "🌰", "Progressive, 0%–3.5%", 0,
            new[] { B(0, 0.0), B(26_050, 0.0275), B(100_000, 0.035) }, 0.0724, 0.0153, 2.5,
            "Low state rate trending toward a flat tax; many municipalities levy their own income tax (not modeled)."),

        Prog("OK", "Oklahoma", "🛢️", "Progressive, 0.25%–4.75%", 6_350,
            new[] { B(0, 0.0025), B(1_000, 0.0075), B(2_500, 0.0175), B(3_750, 0.0275), B(4_900, 0.0375), B(7_200, 0.0475) },
            0.0899, 0.0090, 2.5,
            "Low income tax with high combined sales taxes. Low property taxes."),

        Prog("OR", "Oregon", "🌲", "Progressive, 4.75%–9.9%", 2_745,
            new[] { B(0, 0.0475), B(4_300, 0.0675), B(10_750, 0.0875), B(125_000, 0.099) }, 0.0000, 0.0093, 4.5,
            "No sales tax, so the income tax does the heavy lifting and hits high earners hard."),

        Prog("RI", "Rhode Island", "⛵", "Progressive, 3.75%–5.99%", 10_550,
            new[] { B(0, 0.0375), B(77_450, 0.0475), B(176_050, 0.0599) }, 0.0700, 0.0140, 3.6,
            "Three brackets with a generous standard deduction. Relatively high property taxes."),

        Prog("SC", "South Carolina", "🌴", "Progressive, 0%–6.2%", 14_600,
            new[] { B(0, 0.0), B(3_460, 0.03), B(17_330, 0.062) }, 0.0750, 0.0057, 3.6,
            "A 0% bottom bracket but a high top rate that arrives quickly. Low effective property taxes."),

        None("SD", "South Dakota", "🗿", 0.0611, 0.0117, 2.9,
            "No personal income tax. Relies on sales tax (including on groceries) and a light overall burden."),

        None("TN", "Tennessee", "🎸", 0.0955, 0.0067, 3.4,
            "No tax on wages (the old investment-income tax is gone). Among the highest combined sales-tax rates."),

        Flat("UT", "Utah", "🎿", "Flat 4.55%", 0.0455, 14_600, 0.0725, 0.0057, 4.5,
            "Flat rate paired with a taxpayer credit that phases out for higher earners. Low property taxes."),

        Prog("VT", "Vermont", "🍁", "Progressive, 3.35%–8.75%", 7_000,
            new[] { B(0, 0.0335), B(45_400, 0.066), B(110_050, 0.076), B(229_550, 0.0875) }, 0.0636, 0.0183, 3.8,
            "High top rate and high property taxes that fund a statewide education system."),

        Prog("VA", "Virginia", "🏛️", "Progressive, 2%–5.75%", 8_500,
            new[] { B(0, 0.02), B(3_000, 0.03), B(5_000, 0.05), B(17_000, 0.0575) }, 0.0577, 0.0082, 3.4,
            "The top 5.75% rate kicks in at just $17,000, so most filers pay close to the top rate. Low sales tax."),

        Prog("WV", "West Virginia", "⛏️", "Progressive, 2.36%–5.12%", 0,
            new[] { B(0, 0.0236), B(10_000, 0.0315), B(25_000, 0.0354), B(40_000, 0.0472), B(60_000, 0.0512) },
            0.0657, 0.0058, 2.5,
            "Income-tax rates have been cut in recent years. Low sales and property taxes."),

        Prog("WI", "Wisconsin", "🧀", "Progressive, 3.5%–7.65%", 13_230,
            new[] { B(0, 0.035), B(14_320, 0.044), B(28_640, 0.053), B(315_310, 0.0765) }, 0.0570, 0.0161, 2.9,
            "Wide brackets with a high top rate. Low sales tax but relatively high property taxes."),

        None("WY", "Wyoming", "🦬", 0.0544, 0.0061, 3.4,
            "No personal income tax, funded largely by mineral and energy revenue. Low overall tax burden."),
    };

    private static readonly Dictionary<string, StateProfile> ByCode =
        All.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>Look up a state profile by 2-letter code (case-insensitive). Null if unknown.</summary>
    public static StateProfile? Find(string? code) =>
        code is not null && ByCode.TryGetValue(code, out var p) ? p : null;
}
