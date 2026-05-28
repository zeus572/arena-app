using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Services;

namespace Civic.API.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _current;

    public AuthController(CivicDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    /// <summary>
    /// Rekey civic data created under an anonymous localStorage UUID to the
    /// authenticated user's id after they log in or register. Idempotent — if
    /// the authenticated user already has a profile, the anonymous one is
    /// dropped (the authenticated profile wins).
    /// </summary>
    [Authorize]
    [HttpPost("link-anonymous")]
    public async Task<IActionResult> LinkAnonymous([FromBody] LinkAnonymousRequest body)
    {
        if (!_current.IsAuthenticated) return Unauthorized();

        var authed = _current.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(authed)) return Unauthorized();

        var anon = body.AnonymousUserId?.Trim();
        if (string.IsNullOrWhiteSpace(anon)) return BadRequest(new { error = "anonymousUserId required" });
        if (string.Equals(anon, authed, StringComparison.Ordinal))
            return Ok(new { transferred = new { answers = 0, profiles = 0, budgetSessions = 0, receipts = 0 } });
        if (string.Equals(anon, "anonymous", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "cannot link the literal 'anonymous' id" });

        // CivicAnswers — answers carry the user id directly. Move them to the
        // authed user; if a row already exists for (authedUser, question), keep
        // the authed row and delete the anon one (the unique index would block
        // a duplicate insert otherwise).
        var anonAnswers = await _db.CivicAnswers.Where(a => a.UserId == anon).ToListAsync();
        var existingQuestionIds = await _db.CivicAnswers
            .Where(a => a.UserId == authed)
            .Select(a => a.QuestionId)
            .ToListAsync();

        var movedAnswers = 0;
        foreach (var a in anonAnswers)
        {
            if (existingQuestionIds.Contains(a.QuestionId))
            {
                _db.CivicAnswers.Remove(a);
            }
            else
            {
                a.UserId = authed;
                movedAnswers++;
            }
        }

        // UserProfile — at most one per user (unique index on UserId). If both
        // exist, keep the authed profile and drop the anon one + its axis scores
        // (Cascade handles axis scores).
        var anonProfile = await _db.UserProfiles
            .Include(p => p.AxisScores)
            .FirstOrDefaultAsync(p => p.UserId == anon);
        var authedProfile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == authed);
        var movedProfiles = 0;

        if (anonProfile is not null)
        {
            if (authedProfile is null)
            {
                anonProfile.UserId = authed;
                movedProfiles = 1;
            }
            else
            {
                _db.UserProfiles.Remove(anonProfile);
            }
        }

        // BudgetSessions — many per user; rekey all.
        var movedSessions = await _db.BudgetSessions
            .Where(s => s.UserId == anon)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, _ => authed));

        // ValuesReceipts — many per user; rekey all.
        var movedReceipts = await _db.ValuesReceipts
            .Where(r => r.UserId == anon)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.UserId, _ => authed));

        await _db.SaveChangesAsync();

        return Ok(new
        {
            transferred = new
            {
                answers = movedAnswers,
                profiles = movedProfiles,
                budgetSessions = movedSessions,
                receipts = movedReceipts,
            },
        });
    }

    /// <summary>
    /// Echo the resolved user identity for the caller (handy for the frontend
    /// to confirm a JWT minted by the debate backend is being accepted here).
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = _current.GetCurrentUserId(),
            isAuthenticated = _current.IsAuthenticated,
        });
    }
}

public record LinkAnonymousRequest(string? AnonymousUserId);
