using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Arena.Shared.News;

/// <summary>
/// Name of the HttpClient every news source fetches through. Host apps
/// register it themselves (each sets its own User-Agent / timeout); the name
/// predates the provider architecture, so both apps already have it.
/// </summary>
public static class NewsSourceHttp
{
    public const string ClientName = "RssNewsSource";
}

public static class NewsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the news provider builders + factory. To add a provider:
    /// implement <see cref="INewsSourceBuilder"/>, register it here (or in the
    /// host app), and reference its Kind from config.
    /// </summary>
    public static IServiceCollection AddArenaNewsSources(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INewsSourceBuilder, RssSourceBuilder>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INewsSourceBuilder, GoogleNewsSourceBuilder>());
        services.TryAddSingleton<INewsSourceFactory, NewsSourceFactory>();
        return services;
    }
}
