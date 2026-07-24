using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Models.DTOs;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Read-only feature-engagement aggregate for the admin dashboard: for every key user
/// action, how many distinct real users engaged, how many events, and how recently — so
/// the operator can see where people are engaged vs. not, and by locality.
///
/// COUNTS ONLY. No user id, email, or other PII is returned. Gated by the "Admin" policy
/// (email allowlist in Auth:AdminEmails). Anonymous ("anonymous") and agent rows are
/// excluded from the counts; anonymous volume is surfaced separately as context.
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/admin/engagement")]
public class AdminEngagementController : ControllerBase
{
    private readonly CivicDbContext _db;

    public AdminEngagementController(CivicDbContext db) => _db = db;

    private const int ShortWindowDays = 7;
    private const int LongWindowDays = 30;

    // Area labels (also the display grouping / order).
    private const string Onboarding = "Onboarding";
    private const string Exercises = "Exercises";
    private const string Coalitions = "Coalitions";
    private const string Candidates = "AI candidates";
    private const string Social = "Shorts & posts";
    private const string Groups = "Leagues & circles";
    private const string Petitions = "Petitions";

    private static readonly string[] AreaOrder =
        { Onboarding, Exercises, Coalitions, Candidates, Social, Groups, Petitions };

    /// <summary>Uniform (user, timestamp) projection so every feature can share one aggregator.</summary>
    private sealed class UserEvent
    {
        public string UserId { get; set; } = "";
        public DateTime At { get; set; }
    }

    private sealed class UserAgg
    {
        public string UserId { get; set; } = "";
        public DateTime Last { get; set; }
        public int Count { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<EngagementDto>> Get()
    {
        var now = DateTime.UtcNow;
        var shortCut = now.AddDays(-ShortWindowDays);
        var longCut = now.AddDays(-LongWindowDays);

        // Pull the (UserId, At) rows for one feature, split anonymous out, and fold the rest
        // into one row per user. Grouping is done in memory: the row set scales with users
        // (an admin-only endpoint over a small base), so this stays cheap and avoids any
        // provider-specific GroupBy translation surprises.
        async Task<(List<UserAgg> users, int anon)> Load(IQueryable<UserEvent> q)
        {
            var rows = await q.Where(e => e.UserId != "")
                .Select(e => new { e.UserId, e.At })
                .ToListAsync();
            var anon = rows.Count(r => r.UserId == "anonymous");
            var users = rows.Where(r => r.UserId != "anonymous")
                .GroupBy(r => r.UserId)
                .Select(g => new UserAgg { UserId = g.Key, Last = g.Max(x => x.At), Count = g.Count() })
                .ToList();
            return (users, anon);
        }

        // ---- One deferred query per feature. Projections filter agents and null owners at
        // the source. Executed SEQUENTIALLY below — a DbContext is not thread-safe. ----
        var defs = new (string key, string label, string area, Func<IQueryable<UserEvent>> build)[]
        {
            ("profile",       "Profile / compass built", Onboarding,
                () => _db.UserProfiles.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
            ("compass_answer","Compass questions answered", Onboarding,
                () => _db.CivicAnswers.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
            ("quiz",          "Knowledge quiz answered", Onboarding,
                () => _db.QuizResponses.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),

            ("budget",        "Budget exercise run", Exercises,
                () => _db.BudgetSessions.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
            ("values_receipt","Values receipt generated", Exercises,
                () => _db.ValuesReceipts.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),

            ("coalition_position", "Coalition stance taken", Coalitions,
                () => _db.ProvisionPositions.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
            ("coalition_accept",   "Provision co-signed", Coalitions,
                () => _db.AcceptanceRecords.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
            ("coalition_amend",    "Amendment proposed", Coalitions,
                () => _db.Amendments.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
            ("coalition_join",     "Coalition loop joined", Coalitions,
                () => _db.CoalitionParticipants.Where(x => !x.IsAgent).Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
            ("reasoning_act",      "Reasoning-XP act (any)", Coalitions,
                () => _db.CoalitionActs.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),

            ("candidate_follow", "AI candidate followed", Candidates,
                () => _db.CandidateFollows.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
            ("candidate_mute",   "AI candidate muted", Candidates,
                () => _db.CandidateMutes.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
            ("campaign_run",     "Campaign Manager run", Candidates,
                () => _db.CivicCampaigns.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),

            ("post_authored", "Campaign/short post authored", Social,
                () => _db.CampaignPosts.Where(x => x.OwnerUserId != null).Select(x => new UserEvent { UserId = x.OwnerUserId!, At = x.CreatedAt })),
            ("post_reaction", "Post/short reacted to", Social,
                () => _db.PostReactions.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),

            ("league_owned",  "League created", Groups,
                () => _db.Leagues.Select(x => new UserEvent { UserId = x.OwnerUserId, At = x.CreatedAt })),
            ("league_member", "League joined", Groups,
                () => _db.LeagueMembers.Select(x => new UserEvent { UserId = x.UserId, At = x.JoinedAt })),
            ("league_entry",  "League round entry", Groups,
                () => _db.LeagueRoundEntries.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
            ("cohort_member", "Cohort placement", Groups,
                () => _db.CohortMembers.Where(x => !x.IsAgent).Select(x => new UserEvent { UserId = x.UserId, At = x.JoinedAt })),
            ("circle_member", "Circle placement", Groups,
                () => _db.CoalitionCircleMembers.Select(x => new UserEvent { UserId = x.UserId, At = x.JoinedAt })),

            ("petition_created", "Petition created", Petitions,
                () => _db.Petitions.Select(x => new UserEvent { UserId = x.CreatedBy, At = x.CreatedAt })),
        };

        // ---- Fold results into features + roll up to areas / states / breadth. ----
        var features = new List<FeatureStatDto>();
        var areaUsers = new Dictionary<string, HashSet<string>>();
        var areaActiveLong = new Dictionary<string, HashSet<string>>();
        var userAreas = new Dictionary<string, HashSet<string>>();
        var engaged = new HashSet<string>();
        var activeShort = new HashSet<string>();
        var activeLong = new HashSet<string>();
        var anonEvents = 0;

        foreach (var d in defs)
        {
            var (rows, anon) = await Load(d.build());
            anonEvents += anon;

            features.Add(new FeatureStatDto
            {
                Key = d.key,
                Label = d.label,
                Area = d.area,
                Users = rows.Count,
                Events = rows.Sum(r => r.Count),
                ActiveShort = rows.Count(r => r.Last >= shortCut),
                ActiveLong = rows.Count(r => r.Last >= longCut),
                LastAt = rows.Count > 0 ? DateTime.SpecifyKind(rows.Max(r => r.Last), DateTimeKind.Utc) : null,
            });

            var aUsers = areaUsers.TryGetValue(d.area, out var au) ? au : (areaUsers[d.area] = new());
            var aActive = areaActiveLong.TryGetValue(d.area, out var aa) ? aa : (areaActiveLong[d.area] = new());
            foreach (var r in rows)
            {
                aUsers.Add(r.UserId);
                engaged.Add(r.UserId);
                (userAreas.TryGetValue(r.UserId, out var ua) ? ua : (userAreas[r.UserId] = new())).Add(d.area);
                if (r.Last >= shortCut) activeShort.Add(r.UserId);
                if (r.Last >= longCut) { activeLong.Add(r.UserId); aActive.Add(r.UserId); }
            }
        }

        // Order features by area, then by descending reach.
        features = features
            .OrderBy(f => Array.IndexOf(AreaOrder, f.Area))
            .ThenByDescending(f => f.Users)
            .ThenBy(f => f.Label)
            .ToList();

        var areas = AreaOrder
            .Where(areaUsers.ContainsKey)
            .Select(a => new AreaStatDto
            {
                Area = a,
                Users = areaUsers[a].Count,
                ActiveLong = areaActiveLong[a].Count,
            })
            .ToList();

        // ---- Locality: map users -> state via UserProfiles (null locality => "national"). ----
        var profileRows = await _db.UserProfiles
            .Where(p => p.UserId != "" && p.UserId != "anonymous")
            .Select(p => new { p.UserId, p.LocalityState })
            .ToListAsync();

        var stateByUser = profileRows
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => Normalize(g.First().LocalityState));
        string StateOf(string u) => stateByUser.TryGetValue(u, out var s) ? s : "unknown";

        var profilesByState = profileRows
            .GroupBy(p => Normalize(p.LocalityState))
            .ToDictionary(g => g.Key, g => g.Count());

        var stateKeys = new HashSet<string>(profilesByState.Keys);
        foreach (var u in engaged) stateKeys.Add(StateOf(u));

        var byState = stateKeys
            .OrderByDescending(s => profilesByState.GetValueOrDefault(s))
            .ThenBy(s => s)
            .Select(st =>
            {
                var dto = new StateStatDto
                {
                    State = st,
                    Profiles = profilesByState.GetValueOrDefault(st),
                    EngagedUsers = engaged.Count(u => StateOf(u) == st),
                };
                foreach (var a in areas)
                    dto.ByArea[a.Area] = areaUsers[a.Area].Count(u => StateOf(u) == st);
                return dto;
            })
            .ToList();

        // ---- Breadth: how many areas each engaged user touched. ----
        var breadth = engaged
            .GroupBy(u => userAreas[u].Count)
            .Select(g => new BreadthBucketDto { AreasTouched = g.Key, Users = g.Count() })
            .OrderBy(b => b.AreasTouched)
            .ToList();

        return Ok(new EngagementDto
        {
            GeneratedAt = now,
            ShortWindowDays = ShortWindowDays,
            LongWindowDays = LongWindowDays,
            Summary = new EngagementSummaryDto
            {
                Profiles = profileRows.Count,
                EngagedUsers = engaged.Count,
                ActiveUsersShort = activeShort.Count,
                ActiveUsersLong = activeLong.Count,
                AnonymousEvents = anonEvents,
            },
            Features = features,
            Areas = areas,
            ByState = byState,
            Breadth = breadth,
            Untracked = new List<UntrackedDto>
            {
                new() { Key = "bill_compass", Label = "Bill vs. your Compass",
                    Note = "Computed on the fly from compass vs. bill positions — writes no engagement row. Closest proxy: a completed compass profile." },
                new() { Key = "petition_signatures", Label = "Petition signatures",
                    Note = "Only an aggregate SignatureCount is stored; individual signatures aren't tracked, so signers can't be counted." },
            },
        });
    }

    private static string Normalize(string? state) =>
        string.IsNullOrWhiteSpace(state) ? "national" : state.Trim().ToUpperInvariant();
}
