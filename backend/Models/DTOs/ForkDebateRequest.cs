namespace Arena.API.Models.DTOs;

public class ForkDebateRequest
{
    /// <summary>Optional override topic — defaults to "Re: {parent.Topic}".</summary>
    public string? Topic { get; set; }
    /// <summary>Optional new framing or assumption swap to attach as a fork note.</summary>
    public string? ForkNote { get; set; }
    /// <summary>Optional override format — defaults to parent's format (or arena default).</summary>
    public string? Format { get; set; }
    /// <summary>Optional move to a different arena. Null keeps the parent's arena.</summary>
    public Guid? ArenaId { get; set; }
    /// <summary>Optional new debaters. If null, picks fresh agents.</summary>
    public Guid? ProponentId { get; set; }
    public Guid? OpponentId { get; set; }
}
