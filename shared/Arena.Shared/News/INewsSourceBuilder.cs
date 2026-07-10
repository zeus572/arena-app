namespace Arena.Shared.News;

/// <summary>
/// Builds <see cref="INewsSource"/> instances for one <see cref="NewsSourceConfig.Kind"/>.
/// Register implementations via <c>AddArenaNewsSources()</c>; adding a new
/// provider is one builder class + one registration line + config.
/// </summary>
public interface INewsSourceBuilder
{
    /// <summary>The <see cref="NewsSourceConfig.Kind"/> this builder handles (case-insensitive).</summary>
    string Kind { get; }

    /// <summary>
    /// Builds a source from its config, or returns null (after logging a
    /// warning) when the config is invalid. Must not throw — a bad config
    /// entry must never take down feed construction.
    /// </summary>
    INewsSource? Build(string name, NewsSourceConfig config);
}
