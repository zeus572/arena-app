using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public enum PostTrigger
{
    /// <summary>Reaction to a published Civic Briefing.</summary>
    Briefing,
    /// <summary>Scheduled platform statement between news events.</summary>
    Platform,
    /// <summary>Response to another candidate's post.</summary>
    Response,
    /// <summary>Generic scheduled slot.</summary>
    Scheduled,
}

public enum ReactionType
{
    Up,
    Down,
}

/// <summary>
/// The core content unit: a short (≤160 char) campaign post by a candidate,
/// with a deterministic tone/intensity and 1-3 issue tags. The body is the
/// only LLM-generated field; everything else is decided before the call.
/// </summary>
public class CampaignPost
{
    public Guid Id { get; set; }

    public Guid CandidateId { get; set; }
    public VirtualCandidate? Candidate { get; set; }

    [Required, MaxLength(160)]
    public string Body { get; set; } = "";

    public CampaignTone Tone { get; set; }

    /// <summary>1 (measured) .. 5 (fired up).</summary>
    public int Intensity { get; set; } = 2;

    public string[] IssueTags { get; set; } = Array.Empty<string>();

    public PostTrigger Trigger { get; set; }

    /// <summary>Slug of the Civic Briefing that triggered this post, if any.</summary>
    [MaxLength(160)]
    public string? TriggerBriefingSlug { get; set; }

    /// <summary>Id of the post this one responds to, if any.</summary>
    public Guid? TriggerPostId { get; set; }

    /// <summary>Plank or source title cited by the body (source transparency).</summary>
    [MaxLength(200)]
    public string? CitedReference { get; set; }

    // Aggregate whole-post reaction counters, updated atomically on each write.
    public int UpCount { get; set; }
    public int DownCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<PostFragment> Fragments { get; set; } = new();
}

/// <summary>
/// A clause-level span of a post body that users can react to. Auto-generated
/// when the post is published. start/end are char offsets into Body.
/// </summary>
public class PostFragment
{
    public Guid Id { get; set; }

    public Guid PostId { get; set; }
    public CampaignPost? Post { get; set; }

    [Required, MaxLength(200)]
    public string Text { get; set; } = "";

    public int Start { get; set; }
    public int End { get; set; }

    /// <summary>Ordinal position of the fragment within the post.</summary>
    public int Order { get; set; }

    public int UpCount { get; set; }
    public int DownCount { get; set; }
}

/// <summary>
/// A single user's reaction. Idempotent per (UserId, PostId, FragmentId?):
/// FragmentId null means a whole-post reaction.
/// </summary>
public class PostReaction
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public Guid PostId { get; set; }

    /// <summary>Null = whole-post reaction; otherwise a fragment reaction.</summary>
    public Guid? FragmentId { get; set; }

    public ReactionType Type { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
