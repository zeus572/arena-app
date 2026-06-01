namespace Civic.API.Services.Campaign;

/// <summary>Thrown when a campaign isn't found or isn't owned by the requesting user (→ 404).</summary>
public class CivicCampaignNotFoundException : Exception
{
    public CivicCampaignNotFoundException(string message = "Campaign not found.") : base(message) { }
}

/// <summary>Thrown when a request is invalid given the campaign's state (→ 400).</summary>
public class CivicCampaignValidationException : Exception
{
    public CivicCampaignValidationException(string message) : base(message) { }
}

/// <summary>Thrown when an action conflicts with the campaign's current state (→ 409).</summary>
public class CivicCampaignConflictException : Exception
{
    public CivicCampaignConflictException(string message) : base(message) { }
}

/// <summary>Thrown when a signed-in user is required but the request is anonymous (→ 401).</summary>
public class CivicCampaignAuthRequiredException : Exception
{
    public CivicCampaignAuthRequiredException(string message = "Sign in to manage a campaign.")
        : base(message) { }
}
