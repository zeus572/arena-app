using Microsoft.Extensions.Logging;

namespace Arena.Shared.News;

/// <summary>Builds Google News sources (<see cref="NewsSourceKinds.GoogleNews"/>).</summary>
public class GoogleNewsSourceBuilder : INewsSourceBuilder
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GoogleNewsSourceBuilder> _logger;

    public string Kind => NewsSourceKinds.GoogleNews;

    public GoogleNewsSourceBuilder(IHttpClientFactory httpFactory, ILoggerFactory loggerFactory)
    {
        _httpFactory = httpFactory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GoogleNewsSourceBuilder>();
    }

    public INewsSource? Build(string name, NewsSourceConfig config)
    {
        var missing = config.Feed switch
        {
            GoogleNewsFeedKind.Topic when string.IsNullOrWhiteSpace(config.Topic) => "Topic",
            GoogleNewsFeedKind.Geo when string.IsNullOrWhiteSpace(config.Location) => "Location",
            GoogleNewsFeedKind.Search when string.IsNullOrWhiteSpace(config.Query) => "Query",
            _ => null,
        };
        if (missing is not null)
        {
            _logger.LogWarning("GoogleNewsSourceBuilder: skipping {Name} — Feed={Feed} requires {Field}", name, config.Feed, missing);
            return null;
        }

        return new GoogleNewsSource(
            _httpFactory.CreateClient(NewsSourceHttp.ClientName),
            name,
            config,
            maxEntries: config.MaxEntries ?? 15,
            logger: _loggerFactory.CreateLogger($"GoogleNewsSource[{name}]"));
    }
}
