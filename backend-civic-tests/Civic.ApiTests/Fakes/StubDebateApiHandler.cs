using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Civic.ApiTests.Fakes;

/// <summary>
/// HttpMessageHandler that records calls made to the debate backend and
/// returns a canned response. Used by the DebateInitController tests so the
/// civic backend can be exercised without a real debate service running.
/// </summary>
public class StubDebateApiHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, (HttpStatusCode, string)> _responder;
    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> RequestBodies { get; } = new();
    public List<string?> AuthorizationHeaders { get; } = new();

    public StubDebateApiHandler(HttpStatusCode status, string body)
        : this(_ => (status, body)) { }

    public StubDebateApiHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> responder)
    {
        _responder = responder;
    }

    public static StubDebateApiHandler ThrowsTransport()
        => new(_ => throw new HttpRequestException("debate unreachable"));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());

        var (status, body) = _responder(request);
        var resp = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8),
        };
        resp.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return resp;
    }
}
