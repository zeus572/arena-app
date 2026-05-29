using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services;
using Civic.API.Services.Campaign;

namespace Civic.API.Mapping;

public static class CampaignMappings
{
    public static CandidateSummaryDto ToSummaryDto(this VirtualCandidate c) => new()
    {
        Id = c.Id,
        Slug = c.Slug,
        Name = c.Name,
        Office = c.Office.ToString(),
        State = c.State,
        District = c.District,
        Party = c.Party,
        IsIncumbent = c.IsIncumbent,
        Bio = c.Bio,
        ArchetypeKey = c.ArchetypeKey,
        DefaultTone = c.DefaultTone.ToString(),
        DefaultIntensity = c.DefaultIntensity,
        AvatarBaseUrl = c.AvatarBaseUrl,
        IsFictional = true,
    };

    public static CandidateDetailDto ToDetailDto(this VirtualCandidate c, ICivicCatalog catalog, int postCount)
    {
        var dto = new CandidateDetailDto
        {
            Id = c.Id,
            Slug = c.Slug,
            Name = c.Name,
            Office = c.Office.ToString(),
            State = c.State,
            District = c.District,
            Party = c.Party,
            IsIncumbent = c.IsIncumbent,
            Bio = c.Bio,
            ArchetypeKey = c.ArchetypeKey,
            DefaultTone = c.DefaultTone.ToString(),
            DefaultIntensity = c.DefaultIntensity,
            AvatarBaseUrl = c.AvatarBaseUrl,
            IsFictional = true,
            Background = c.Background,
            PostCount = postCount,
            PlatformPlanks = c.PlatformPlanks.Select(p => p.ToDto()).ToList(),
            Values = c.AxisScores.ToValueDtos(catalog),
            IssueTones = c.IssueTones.Select(t => t.ToDto()).ToList(),
        };
        return dto;
    }

    public static PlatformPlankDto ToDto(this PlatformPlank p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        Body = p.Body,
        IssueTags = p.IssueTags,
    };

    public static CandidateSourceDto ToDto(this CandidateSource s) => new()
    {
        Id = s.Id,
        Kind = s.Kind.ToString(),
        Title = s.Title,
        Excerpt = s.Excerpt,
        IssueTags = s.IssueTags,
        Priority = s.Priority,
    };

    public static List<CandidateValueDto> ToValueDtos(this IEnumerable<CandidateAxisScore> scores, ICivicCatalog catalog)
    {
        var byKey = scores.ToDictionary(s => s.AxisKey, s => s.Score);
        return catalog.Axes
            .OrderBy(a => a.Order)
            .Select(a => new CandidateValueDto
            {
                AxisKey = a.Key,
                AxisName = a.Name,
                LowLabel = a.LowLabel,
                HighLabel = a.HighLabel,
                Order = a.Order,
                Score = byKey.TryGetValue(a.Key, out var v) ? v : 0,
            })
            .ToList();
    }

    public static IssueToneDto ToDto(this CandidateIssueTone t) => new()
    {
        Issue = t.Issue,
        Tone = t.Tone.ToString(),
        ToneLabel = CampaignToneGuide.Label(t.Tone),
        Intensity = t.Intensity,
        IntensityLabel = CampaignToneGuide.IntensityLabel(t.Intensity),
    };

    public static PostFragmentDto ToDto(this PostFragment f) => new()
    {
        Id = f.Id,
        Text = f.Text,
        Start = f.Start,
        End = f.End,
        Order = f.Order,
        Up = f.UpCount,
        Down = f.DownCount,
    };

    public static CampaignPostDto ToDto(
        this CampaignPost p,
        VirtualCandidate? candidate = null,
        string? briefingHeadline = null)
    {
        return new CampaignPostDto
        {
            Id = p.Id,
            Body = p.Body,
            Tone = p.Tone.ToString(),
            ToneLabel = CampaignToneGuide.Label(p.Tone),
            Intensity = p.Intensity,
            IntensityLabel = CampaignToneGuide.IntensityLabel(p.Intensity),
            IssueTags = p.IssueTags,
            Trigger = p.Trigger.ToString(),
            TriggerBriefingSlug = p.TriggerBriefingSlug,
            TriggerBriefingHeadline = briefingHeadline,
            TriggerPostId = p.TriggerPostId,
            CitedReference = p.CitedReference,
            Up = p.UpCount,
            Down = p.DownCount,
            CreatedAt = DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc),
            Candidate = candidate?.ToSummaryDto(),
            Fragments = p.Fragments
                .OrderBy(f => f.Order)
                .Select(f => f.ToDto())
                .ToList(),
        };
    }

    public static PostHeatmapDto ToHeatmapDto(this CampaignPost p) => new()
    {
        PostId = p.Id,
        Body = p.Body,
        Fragments = p.Fragments
            .OrderBy(f => f.Order)
            .Select(f =>
            {
                var total = f.UpCount + f.DownCount;
                return new HeatmapFragmentDto
                {
                    Id = f.Id,
                    Text = f.Text,
                    Start = f.Start,
                    End = f.End,
                    Order = f.Order,
                    Up = f.UpCount,
                    Down = f.DownCount,
                    Net = total == 0 ? 0 : (double)(f.UpCount - f.DownCount) / total,
                };
            })
            .ToList(),
    };

    public static ElectionCycleDto ToDto(this ElectionCycle c) => new()
    {
        Id = c.Id,
        Slug = c.Slug,
        Name = c.Name,
        ElectionDate = DateTime.SpecifyKind(c.ElectionDate, DateTimeKind.Utc),
        PrimarySeasonStart = DateTime.SpecifyKind(c.PrimarySeasonStart, DateTimeKind.Utc),
        GeneralSeasonStart = DateTime.SpecifyKind(c.GeneralSeasonStart, DateTimeKind.Utc),
        IsCurrent = c.IsCurrent,
        DaysUntilElection = (int)Math.Ceiling((c.ElectionDate - DateTime.UtcNow).TotalDays),
    };
}
