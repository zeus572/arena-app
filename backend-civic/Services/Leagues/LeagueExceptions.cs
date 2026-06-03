namespace Civic.API.Services.Leagues;

/// <summary>Thrown when a league/round/entry isn't found, or the user isn't a member (→ 404).</summary>
public class LeagueNotFoundException : Exception
{
    public LeagueNotFoundException(string message = "League not found.") : base(message) { }
}

/// <summary>Thrown when a request is invalid given the league's state (→ 400).</summary>
public class LeagueValidationException : Exception
{
    public LeagueValidationException(string message) : base(message) { }
}

/// <summary>Thrown when an action conflicts with current state, incl. a full/expired invite (→ 409).</summary>
public class LeagueConflictException : Exception
{
    public LeagueConflictException(string message) : base(message) { }
}

/// <summary>Thrown when an invite code is no longer usable — expired, revoked, or exhausted (→ 410).</summary>
public class LeagueInviteGoneException : Exception
{
    public LeagueInviteGoneException(string message = "This invite is no longer valid.") : base(message) { }
}

/// <summary>Thrown when a member lacks the role for an owner-only action (→ 403).</summary>
public class LeagueForbiddenException : Exception
{
    public LeagueForbiddenException(string message = "Only the league owner can do that.") : base(message) { }
}

/// <summary>Thrown when a signed-in user is required but the request is anonymous (→ 401).</summary>
public class LeagueAuthRequiredException : Exception
{
    public LeagueAuthRequiredException(string message = "Sign in to use leagues.") : base(message) { }
}
