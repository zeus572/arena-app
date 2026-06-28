using System.Net;
using System.Text;

namespace Arena.UnitTests.Social;

/// <summary>
/// Records requests and returns canned responses keyed by the XRPC method in the path.
/// Lets Gate 3 exercise session/refresh/publish with no live network.
/// </summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<string, int, (HttpStatusCode, string)> _responder;
    public List<string> Calls { get; } = new();
    private readonly Dictionary<string, int> _counts = new();

    public StubHttpHandler(Func<string, int, (HttpStatusCode, string)> responder)
        => _responder = responder;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var method = path.Split('/').Last(); // e.g. com.atproto.repo.createRecord
        _counts.TryGetValue(method, out var n);
        _counts[method] = n + 1;
        Calls.Add(method);

        var (status, body) = _responder(method, n);
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }

    public int CountFor(string method) => Calls.Count(c => c == method);
}
