namespace Arena.API.Models;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DebateTag> DebateTags { get; set; } = new List<DebateTag>();
}

public class DebateTag
{
    public Guid DebateId { get; set; }
    public Debate Debate { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
