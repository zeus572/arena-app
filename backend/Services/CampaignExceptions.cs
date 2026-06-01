namespace Arena.API.Services;

/// <summary>Thrown when a campaign cannot be found or is not owned by the current user.</summary>
public class CampaignNotFoundException : Exception
{
    public CampaignNotFoundException(string message = "Campaign not found.") : base(message) { }
}

/// <summary>Thrown when a request is invalid (bad input, unaffordable allocation, wrong state).</summary>
public class CampaignValidationException : Exception
{
    public CampaignValidationException(string message) : base(message) { }
}

/// <summary>Thrown when an action conflicts with the campaign's current state.</summary>
public class CampaignConflictException : Exception
{
    public CampaignConflictException(string message) : base(message) { }
}
