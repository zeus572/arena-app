namespace Civic.API.Models.DTOs;

/// <summary>
/// "What people are discovering" — aggregate signals from how the public is governing
/// itself on Civersify: where their Civic Compasses point, which coalition positions are
/// prevailing, and which civic ideas trip people up. Designed to be readable by leaders.
/// </summary>
public class ZeitgeistDto
{
    public DateTime GeneratedAt { get; set; }
    public ZeitgeistTotalsDto Totals { get; set; } = new();
    public List<ZeitgeistAxisDto> Axes { get; set; } = new();
    public List<ZeitgeistCoalitionDto> Coalitions { get; set; } = new();
    public List<ZeitgeistQuizSignalDto> QuizSignals { get; set; } = new();
}

public class ZeitgeistTotalsDto
{
    public int ProfileCount { get; set; }
    public int CoalitionCount { get; set; }
    public int QuizResponseCount { get; set; }
}

/// <summary>The public's prevailing lean on one Civic Compass axis.</summary>
public class ZeitgeistAxisDto
{
    public string AxisKey { get; set; } = "";
    public string AxisName { get; set; } = "";
    public string LowLabel { get; set; } = "";
    public string HighLabel { get; set; } = "";
    /// <summary>Mean of all scored profiles on this axis, in [-1, 1].</summary>
    public double AverageScore { get; set; }
    /// <summary>Plain-language read: which side the public leans, or "Split".</summary>
    public string LeanLabel { get; set; } = "";
    /// <summary>How many profiles contributed a score to this axis.</summary>
    public int SampleSize { get; set; }
}

/// <summary>A coalition forming around a real-world provision and its prevailing wording.</summary>
public class ZeitgeistCoalitionDto
{
    public Guid ProvisionId { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string State { get; set; } = "";
    public string PrevailingPosition { get; set; } = "";
    public int Accepts { get; set; }
    public int Declines { get; set; }
    public int ParticipantCount { get; set; }
    /// <summary>One-line "what this tells leaders".</summary>
    public string Signal { get; set; } = "";
}

/// <summary>A civic idea people most often get wrong (lowest 60-day correct rate).</summary>
public class ZeitgeistQuizSignalDto
{
    public string Topic { get; set; } = "";
    public string Question { get; set; } = "";
    public double CorrectRate { get; set; }
    public int ResponseCount { get; set; }
}
