using Civic.API.Models;
using Civic.API.Models.DTOs;

namespace Civic.API.Services.Leagues;

/// <summary>
/// Turns a league's members into ranked season standings. The score is deliberately simple and
/// explainable: <c>LeagueScore = RoundPoints + CampaignScore</c>.
///
/// <list type="bullet">
///   <item>RoundPoints — peer-vote competition. Accumulated when shared rounds close and cached on
///   <see cref="LeagueMember.SeasonPoints"/> (winner +5, other entrants +1).</item>
///   <item>CampaignScore — the member's ongoing Campaign Manager run: their candidate's current
///   support share scaled to 0..10, plus a +3 bonus for a completed, won campaign.</item>
/// </list>
///
/// Weights are placeholders, tuned after playtest.
/// </summary>
public class LeagueScoringService
{
    public const int WinnerPoints = 5;
    public const int EntrantPoints = 1;
    public const int WonCampaignBonus = 3;

    /// <param name="campaignsById">
    /// Linked campaigns keyed by id, each with <see cref="CivicCampaign.Standings"/> loaded.
    /// </param>
    public List<LeagueStandingDto> ComputeStandings(
        League league,
        IReadOnlyDictionary<Guid, CivicCampaign> campaignsById,
        string meUserId)
    {
        var rows = league.Members.Select(m =>
        {
            CivicCampaign? campaign = null;
            if (m.CampaignId is Guid cid)
                campaignsById.TryGetValue(cid, out campaign);

            var (campaignScore, support, won) = CampaignScore(campaign);
            var roundPoints = m.SeasonPoints;

            return new LeagueStandingDto
            {
                MemberId = m.Id,
                UserId = m.UserId,
                DisplayName = m.DisplayName,
                AvatarUrl = m.AvatarUrl,
                CandidateName = m.Candidate?.Name,
                Party = m.Candidate?.Party,
                LeagueScore = roundPoints + campaignScore,
                RoundPoints = roundPoints,
                CampaignScore = campaignScore,
                SupportShare = support,
                Won = won,
                IsMe = m.UserId == meUserId,
            };
        }).ToList();

        // Rank desc by score; tie-break by support share so an active campaign edges an idle one.
        var ranked = rows
            .OrderByDescending(r => r.LeagueScore)
            .ThenByDescending(r => r.SupportShare ?? 0)
            .ThenBy(r => r.DisplayName)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
            ranked[i].Rank = i + 1;

        return ranked;
    }

    private static (int score, double? support, bool? won) CampaignScore(CivicCampaign? campaign)
    {
        if (campaign is null) return (0, null, null);

        var player = campaign.Standings.FirstOrDefault(s => s.IsPlayer);
        var support = player is null ? 0 : Math.Round(player.SupportShare, 1);

        var score = (int)Math.Round(support / 10.0);
        if (campaign.Status == CivicCampaignStatus.Completed && campaign.Won == true)
            score += WonCampaignBonus;

        return (score, support, campaign.Won);
    }
}
