using Microsoft.AspNetCore.Mvc;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class SourcesController : ControllerBase
{
    private static readonly List<object> Sources = new()
    {
        new
        {
            Id = "usafacts",
            Name = "USAFacts",
            Url = "https://usafacts.org",
            Category = "Government Data",
            Description = "Non-partisan source for verified US government data, statistics, and facts. Covers spending, demographics, economic data, education, healthcare, crime, and other quantitative claims.",
            Icon = "bar-chart",
        },
        new
        {
            Id = "usaspending",
            Name = "USASpending.gov",
            Url = "https://www.usaspending.gov",
            Category = "Federal Budget",
            Description = "Official US government source for federal spending data. Provides agency budgets, budget authority, obligations, outlays, and program-level spending from the Department of the Treasury.",
            Icon = "landmark",
        },
        new
        {
            Id = "wikipedia",
            Name = "Wikipedia",
            Url = "https://en.wikipedia.org",
            Category = "General Knowledge",
            Description = "Background information, historical context, definitions, and general knowledge. Used for understanding concepts, historical events, policy background, and broad factual context.",
            Icon = "book-open",
        },
        new
        {
            Id = "duckduckgo",
            Name = "DuckDuckGo Web Search",
            Url = "https://duckduckgo.com",
            Category = "Web Search",
            Description = "Web search for recent news, studies, reports, and current events. Provides up-to-date sources including news articles, academic papers, think tank reports, and government publications.",
            Icon = "globe",
        },
    };

    [HttpGet]
    public IActionResult GetSources()
    {
        return Ok(new { citations = Sources, news = NewsSources });
    }

    private static readonly List<object> NewsSources = new()
    {
        new
        {
            Id = "npr",
            Name = "NPR News",
            Url = "https://npr.org",
            Category = "News",
            Description = "National Public Radio — non-profit, publicly funded news organization providing balanced reporting on politics, policy, and current events.",
            Icon = "radio",
        },
        new
        {
            Id = "ap",
            Name = "Associated Press",
            Url = "https://apnews.com",
            Category = "News",
            Description = "Non-profit news agency known for factual, unbiased reporting. One of the most trusted wire services worldwide, used by newsrooms globally.",
            Icon = "newspaper",
        },
        new
        {
            Id = "bbc",
            Name = "BBC News",
            Url = "https://bbc.com/news",
            Category = "News",
            Description = "British Broadcasting Corporation — publicly funded international news service providing global coverage of politics, economics, and world events.",
            Icon = "globe",
        },
    };
}
