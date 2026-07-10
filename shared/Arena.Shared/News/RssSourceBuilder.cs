using Microsoft.Extensions.Logging;

namespace Arena.Shared.News;

/// <summary>Builds plain RSS/Atom sources (<see cref="NewsSourceKinds.Rss"/>).</summary>
public class RssSourceBuilder : INewsSourceBuilder
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RssSourceBuilder> _logger;

    public string Kind => NewsSourceKinds.Rss;

    public RssSourceBuilder(IHttpClientFactory httpFactory, ILoggerFactory loggerFactory)
    {
        _httpFactory = httpFactory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<RssSourceBuilder>();
    }

    public INewsSource? Build(string name, NewsSourceConfig config)
    {
        if (!Uri.TryCreate(config.Url, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("RssSourceBuilder: skipping {Name} — Url is missing or not absolute: {Url}", name, config.Url);
            return null;
        }

        return new RssNewsSource(
            _httpFactory.CreateClient(NewsSourceHttp.ClientName),
            name,
            uri,
            maxEntries: config.MaxEntries ?? 15,
            logger: _loggerFactory.CreateLogger($"RssNewsSource[{name}]"));
    }
}
