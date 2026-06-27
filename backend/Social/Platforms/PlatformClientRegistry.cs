namespace Arena.API.Social.Platforms;

/// <summary>
/// Resolves <see cref="IPlatformClient"/> instances by key. At launch only "bluesky" is registered;
/// the registry is the single seam future adapters (X, IG/Threads/LinkedIn) drop into.
/// // Deferred: additional platforms — see §1 / §4.2.
/// </summary>
public sealed class PlatformClientRegistry : IPlatformClientRegistry
{
    private readonly Dictionary<string, IPlatformClient> _clients;

    public PlatformClientRegistry(IEnumerable<IPlatformClient> clients)
        => _clients = clients.ToDictionary(c => c.PlatformKey, c => c, StringComparer.Ordinal);

    public bool TryGet(string platformKey, out IPlatformClient client)
        => _clients.TryGetValue(platformKey, out client!);

    public IReadOnlyCollection<string> Keys => _clients.Keys.ToList();
}
