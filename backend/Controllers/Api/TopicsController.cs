using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Models.DTOs;
using Arena.API.Services;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class TopicsController : ControllerBase
{
    private readonly ArenaDbContext _db;
    private readonly TopicModerationService _moderation;

    public TopicsController(ArenaDbContext db, TopicModerationService moderation)
    {
        _db = db;
        _moderation = moderation;
    }

    /// <summary>
    /// Ensures there are enough topic proposals for voting by pulling from the GeneratedTopics pool.
    /// </summary>
    private async Task EnsureSuggestedTopicsAsync(int minCount = 15)
    {
        var currentCount = await _db.TopicProposals
            .CountAsync(t => t.Status == TopicStatus.Proposed);

        if (currentCount >= minCount) return;

        var needed = minCount - currentCount;
        var existingTitles = await _db.TopicProposals
            .Select(t => t.Title.ToLower())
            .ToListAsync();
        var existingSet = new HashSet<string>(existingTitles, StringComparer.OrdinalIgnoreCase);

        // Pull unused topics, prefer news over static
        var candidates = await _db.GeneratedTopics
            .Where(t => !t.Used)
            .OrderByDescending(t => t.Source == "news" ? 1 : 0)
            .ThenBy(_ => EF.Functions.Random())
            .Take(needed + 10) // grab extra in case of dupes
            .ToListAsync();

        // Need a system user for auto-proposed topics
        var systemUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == "system");
        if (systemUser is null)
        {
            systemUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "system",
                Email = "system@arena.local",
                DisplayName = "Debate Arena",
                IsAnonymous = false,
            };
            _db.Users.Add(systemUser);
            await _db.SaveChangesAsync();
        }

        var added = 0;
        foreach (var candidate in candidates)
        {
            if (added >= needed) break;
            if (existingSet.Contains(candidate.Title)) continue;

            _db.TopicProposals.Add(new TopicProposal
            {
                Id = Guid.NewGuid(),
                Title = candidate.Title,
                Description = candidate.Source == "news" ? "Generated from recent news" : null,
                ProposedByUserId = systemUser.Id,
                Status = TopicStatus.Proposed,
            });

            candidate.Used = true;
            existingSet.Add(candidate.Title);
            added++;
        }

        if (added > 0) await _db.SaveChangesAsync();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "hot",
        [FromQuery] string? status = null)
    {
        // Auto-populate proposals from the generated topics pool
        await EnsureSuggestedTopicsAsync();

        var query = _db.TopicProposals.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TopicStatus>(status, true, out var statusEnum))
            query = query.Where(t => t.Status == statusEnum);

        var totalCount = await query.CountAsync();

        // "hot" uses net votes + recency; computed via net votes then recency as tiebreaker
        query = sort.ToLowerInvariant() switch
        {
            "new" => query.OrderByDescending(t => t.CreatedAt),
            "top" => query.OrderByDescending(t => t.UpvoteCount - t.DownvoteCount)
                .ThenByDescending(t => t.CreatedAt),
            _ => query // "hot": high net votes + recent
                .OrderByDescending(t => t.UpvoteCount - t.DownvoteCount)
                .ThenByDescending(t => t.CreatedAt),
        };

        // Get current user's votes if authenticated
        Guid? currentUserId = null;
        var sub = User.FindFirst("sub")?.Value;
        if (sub is not null && Guid.TryParse(sub, out var uid))
            currentUserId = uid;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Description,
                Status = t.Status.ToString(),
                t.UpvoteCount,
                t.DownvoteCount,
                NetVotes = t.UpvoteCount - t.DownvoteCount,
                ProposedBy = new { t.ProposedByUser.Id, t.ProposedByUser.DisplayName },
                t.CreatedAt,
                UserVote = currentUserId != null
                    ? t.TopicVotes
                        .Where(v => v.UserId == currentUserId)
                        .Select(v => (int?)v.Value)
                        .FirstOrDefault()
                    : null,
            })
            .ToListAsync();

        return Ok(new { items, totalCount });
    }

    [Authorize(Policy = "Premium")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTopicRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length < 10)
            return BadRequest(new { error = "Title must be at least 10 characters." });

        var rejection = await _moderation.CheckTopicAsync(request.Title.Trim());
        if (rejection is not null)
            return BadRequest(new { error = rejection });

        var userId = Guid.Parse(User.FindFirst("sub")!.Value);

        var topic = new TopicProposal
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            ProposedByUserId = userId,
            Status = TopicStatus.Proposed,
        };

        _db.TopicProposals.Add(topic);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { }, new { topic.Id, topic.Title });
    }

    [Authorize]
    [HttpPost("{id:guid}/vote")]
    public async Task<IActionResult> Vote(Guid id, [FromBody] TopicVoteRequest request)
    {
        if (request.Value is not (1 or -1))
            return BadRequest(new { error = "Value must be 1 or -1." });

        var topic = await _db.TopicProposals.FindAsync(id);
        if (topic is null) return NotFound();

        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var existing = await _db.TopicVotes
            .FirstOrDefaultAsync(v => v.TopicProposalId == id && v.UserId == userId);

        if (existing is not null)
        {
            // Update existing vote
            if (existing.Value == request.Value)
                return Ok(new { status = "already voted" });

            // Adjust counts
            if (existing.Value == 1) topic.UpvoteCount--;
            else topic.DownvoteCount--;

            existing.Value = request.Value;
        }
        else
        {
            _db.TopicVotes.Add(new TopicVote
            {
                Id = Guid.NewGuid(),
                TopicProposalId = id,
                UserId = userId,
                Value = request.Value,
            });
        }

        if (request.Value == 1) topic.UpvoteCount++;
        else topic.DownvoteCount++;

        topic.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { topic.UpvoteCount, topic.DownvoteCount, NetVotes = topic.UpvoteCount - topic.DownvoteCount });
    }

    [Authorize]
    [HttpDelete("{id:guid}/vote")]
    public async Task<IActionResult> RemoveVote(Guid id)
    {
        var topic = await _db.TopicProposals.FindAsync(id);
        if (topic is null) return NotFound();

        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var existing = await _db.TopicVotes
            .FirstOrDefaultAsync(v => v.TopicProposalId == id && v.UserId == userId);

        if (existing is null)
            return Ok(new { status = "no vote to remove" });

        if (existing.Value == 1) topic.UpvoteCount--;
        else topic.DownvoteCount--;

        _db.TopicVotes.Remove(existing);
        topic.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { topic.UpvoteCount, topic.DownvoteCount, NetVotes = topic.UpvoteCount - topic.DownvoteCount });
    }
}
