namespace Arena.API.Models;

public enum TopicStatus
{
    Proposed = 0,
    Approved = 1,
    Rejected = 2,
    Debated = 3
}

public class TopicProposal
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TopicStatus Status { get; set; } = TopicStatus.Proposed;
    public Guid ProposedByUserId { get; set; }
    public User ProposedByUser { get; set; } = null!;
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TopicVote> TopicVotes { get; set; } = new List<TopicVote>();
}

public class TopicVote
{
    public Guid Id { get; set; }
    public Guid TopicProposalId { get; set; }
    public TopicProposal TopicProposal { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int Value { get; set; } // +1 or -1
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
