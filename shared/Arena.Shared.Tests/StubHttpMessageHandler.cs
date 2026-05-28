using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Arena.Shared.Tests;

/// <summary>
/// Drop-in HttpMessageHandler that responds to GET requests with the body
/// returned by <paramref name="getResponder"/> (keyed on request URI) and to
/// POST requests with <paramref name="postResponder"/> (passed the parsed body).
/// Use it to wire a real HttpClient against fake remote services in tests.
/// </summary>
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<(HttpStatusCode, string, string)>> _handler;
    public List<HttpRequestMessage> Requests { get; } = new();

    /// <summary>
    /// Captured request body strings (buffered before the caller disposes the
    /// HttpRequestMessage). Indexed the same as <see cref="Requests"/>.
    /// </summary>
    public List<string> RequestBodies { get; } = new();

    public StubHttpMessageHandler(Func<HttpRequestMessage, Task<(HttpStatusCode, string, string)>> handler)
    {
        _handler = handler;
    }

    public static StubHttpMessageHandler FromBody(string body, string mediaType = "application/json", HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => Task.FromResult((status, body, mediaType)));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));

        var (status, body, mediaType) = await _handler(request);
        var resp = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8),
        };
        resp.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return resp;
    }
}
