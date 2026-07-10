using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arena.Shared.News;

/// <summary>
/// Turns typed source config (<see cref="NewsSourceConfig"/>) into
/// <see cref="INewsSource"/>s by dispatching to the registered
/// <see cref="INewsSourceBuilder"/> for each entry's Kind. Invalid entries are
/// skipped with a warning — a config typo must never take down the pipeline.
/// </summary>
public interface INewsSourceFactory
{
    /// <summary>Builds one source, or null when disabled or invalid.</summary>
    INewsSource? TryCreate(string name, NewsSourceConfig config);

    /// <summary>Builds an aggregate feed over every valid, enabled source in <paramref name="sources"/>.</summary>
    INewsFeed CreateFeed(IReadOnlyDictionary<string, NewsSourceConfig> sources);
}

public class NewsSourceFactory : INewsSourceFactory
{
    private readonly Dictionary<string, INewsSourceBuilder> _builders;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<NewsSourceFactory> _logger;

    public NewsSourceFactory(IEnumerable<INewsSourceBuilder> builders, ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<NewsSourceFactory>();
        // Last registration wins so a host app can override a built-in Kind.
        _builders = new Dictionary<string, INewsSourceBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var builder in builders)
        {
            _builders[builder.Kind] = builder;
        }
    }

    public INewsSource? TryCreate(string name, NewsSourceConfig config)
    {
        if (!config.Enabled)
        {
            return null;
        }

        if (!_builders.TryGetValue(config.Kind, out var builder))
        {
            _logger.LogWarning(
                "NewsSourceFactory: skipping {Name} — unknown Kind {Kind} (registered: {Kinds})",
                name, config.Kind, string.Join(", ", _builders.Keys));
            return null;
        }

        return builder.Build(name, config);
    }

    public INewsFeed CreateFeed(IReadOnlyDictionary<string, NewsSourceConfig> sources)
    {
        var built = sources
            .Select(kv => TryCreate(kv.Key, kv.Value))
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        _logger.LogInformation(
            "NewsSourceFactory: built {Built}/{Configured} sources: {Names}",
            built.Count, sources.Count, string.Join(", ", built.Select(s => s.Name)));

        return new AggregateNewsFeed(built, _loggerFactory.CreateLogger<AggregateNewsFeed>());
    }
}
