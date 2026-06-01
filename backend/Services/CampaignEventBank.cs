using Arena.API.Models;

namespace Arena.API.Services;

/// <summary>A response option on a campaign event, with its resource/approval effects.</summary>
public class CampaignEventOption
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double Approval { get; set; }
    public double Budget { get; set; }
    public double Momentum { get; set; }
}

/// <summary>A templated campaign event with a set of response options.</summary>
public class CampaignEventTemplate
{
    public string EventKey { get; set; } = string.Empty;
    public CampaignEventType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<CampaignEventOption> Options { get; set; } = new();
}

/// <summary>
/// Static bank of templated campaign events. No LLM needed — fully template-based.
/// </summary>
public static class CampaignEventBank
{
    public static readonly IReadOnlyList<CampaignEventTemplate> All = new List<CampaignEventTemplate>
    {
        new()
        {
            EventKey = "scandal",
            Type = CampaignEventType.Crisis,
            Title = "A Damaging Story Breaks",
            Description = "A reporter is about to publish an unflattering story about your campaign. How do you respond?",
            Options = new()
            {
                new() { Id = "deny", Label = "Flatly deny and attack the source", Approval = -4, Budget = 0, Momentum = -5 },
                new() { Id = "address", Label = "Get ahead of it with a candid statement", Approval = -1, Budget = -5000, Momentum = 3 },
                new() { Id = "ignore", Label = "Ignore it and hope it blows over", Approval = -3, Budget = 0, Momentum = -2 },
            },
        },
        new()
        {
            EventKey = "endorsement",
            Type = CampaignEventType.Opportunity,
            Title = "A Popular Figure Offers an Endorsement",
            Description = "A well-liked local figure wants to endorse you — for a price in time and money.",
            Options = new()
            {
                new() { Id = "accept", Label = "Accept and hold a joint rally", Approval = 5, Budget = -8000, Momentum = 6 },
                new() { Id = "modest", Label = "Accept a quiet written endorsement", Approval = 2, Budget = -1000, Momentum = 2 },
                new() { Id = "decline", Label = "Politely decline", Approval = 0, Budget = 0, Momentum = -1 },
            },
        },
        new()
        {
            EventKey = "viral",
            Type = CampaignEventType.Opportunity,
            Title = "A Clip Goes Viral",
            Description = "A clip of you is spreading fast online. You can lean in or play it safe.",
            Options = new()
            {
                new() { Id = "amplify", Label = "Pour ad money into amplifying it", Approval = 4, Budget = -6000, Momentum = 8 },
                new() { Id = "ride", Label = "Let it ride organically", Approval = 2, Budget = 0, Momentum = 4 },
                new() { Id = "downplay", Label = "Downplay it to avoid backlash", Approval = 0, Budget = 0, Momentum = 1 },
            },
        },
        new()
        {
            EventKey = "budget-shortfall",
            Type = CampaignEventType.Crisis,
            Title = "A Budget Shortfall",
            Description = "Your finance director warns that you are burning cash faster than planned.",
            Options = new()
            {
                new() { Id = "cut", Label = "Cut spending hard this week", Approval = -2, Budget = 4000, Momentum = -2 },
                new() { Id = "emergency", Label = "Run an emergency fundraising push", Approval = -1, Budget = 6000, Momentum = -3 },
                new() { Id = "borrow", Label = "Borrow against future donations", Approval = 0, Budget = 10000, Momentum = -4 },
            },
        },
        new()
        {
            EventKey = "town-hall-invite",
            Type = CampaignEventType.Opportunity,
            Title = "An Invitation to a Community Town Hall",
            Description = "A community group invites you to an unscheduled town hall with engaged voters.",
            Options = new()
            {
                new() { Id = "attend", Label = "Attend and connect with voters", Approval = 4, Budget = -2000, Momentum = 4 },
                new() { Id = "surrogate", Label = "Send a surrogate in your place", Approval = 1, Budget = -500, Momentum = 0 },
                new() { Id = "skip", Label = "Skip it to focus elsewhere", Approval = -1, Budget = 0, Momentum = -1 },
            },
        },
        new()
        {
            EventKey = "opponent-stumble",
            Type = CampaignEventType.Neutral,
            Title = "Your Opponent Stumbles",
            Description = "Your opponent made an awkward gaffe. How aggressively do you capitalize?",
            Options = new()
            {
                new() { Id = "pounce", Label = "Pounce with attack ads", Approval = 3, Budget = -7000, Momentum = 5 },
                new() { Id = "subtle", Label = "Make a subtle, classy contrast", Approval = 2, Budget = -1000, Momentum = 3 },
                new() { Id = "highroad", Label = "Take the high road and say nothing", Approval = 1, Budget = 0, Momentum = 1 },
            },
        },
    };

    /// <summary>
    /// Pick a random event template, avoiding keys used recently when possible.
    /// </summary>
    public static CampaignEventTemplate Pick(Random random, IEnumerable<string> recentKeys)
    {
        var recent = new HashSet<string>(recentKeys, StringComparer.OrdinalIgnoreCase);
        var fresh = All.Where(e => !recent.Contains(e.EventKey)).ToList();
        var pool = fresh.Count > 0 ? fresh : All.ToList();
        return pool[random.Next(pool.Count)];
    }

    public static CampaignEventTemplate? Find(string eventKey)
        => All.FirstOrDefault(e => string.Equals(e.EventKey, eventKey, StringComparison.OrdinalIgnoreCase));

    public static CampaignEventOption? FindOption(string eventKey, string optionId)
        => Find(eventKey)?.Options.FirstOrDefault(o => string.Equals(o.Id, optionId, StringComparison.OrdinalIgnoreCase));
}
