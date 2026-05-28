using System.Net;
using System.Reflection;
using Arena.Shared.News;
using FluentAssertions;
using Xunit;

namespace Arena.Shared.Tests;

public class RssNewsSourceTests
{
    private static string LoadFixture(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = asm.GetManifestResourceNames()
            .First(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task FetchAsync_ParsesItemsAndAppliesMinTitleLength()
    {
        var xml = LoadFixture("sample-rss.xml");
        var handler = StubHttpMessageHandler.FromBody(xml, "application/xml");
        var http = new HttpClient(handler);

        var src = new RssNewsSource(http, "Sample", new Uri("https://example.com/news/rss.xml"));
        var items = await src.FetchAsync();

        items.Should().HaveCount(2, "the 'Short' item is below the default min title length of 15");
        items.Should().OnlyContain(i => i.Source == "Sample");
        items.Should().Contain(i => i.Headline.Contains("student data privacy"));
        items.Should().Contain(i => i.Headline.Contains("universal injunctions"));
        items[0].PublishedAt.Should().NotBe(default);
        items[0].ExternalId.Should().NotBeNullOrWhiteSpace();
        items[0].Url.Should().StartWith("https://");
    }

    [Fact]
    public async Task FetchAsync_NetworkFailure_ReturnsEmpty_AndDoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("boom"));
        var http = new HttpClient(handler);

        var src = new RssNewsSource(http, "Sample", new Uri("https://example.com/rss.xml"));
        var items = await src.FetchAsync();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task AggregateNewsFeed_DedupesAcrossSources()
    {
        var xml = LoadFixture("sample-rss.xml");
        var http = new HttpClient(StubHttpMessageHandler.FromBody(xml, "application/xml"));
        var a = new RssNewsSource(http, "A", new Uri("https://example.com/a.xml"));
        var b = new RssNewsSource(http, "B", new Uri("https://example.com/b.xml"));

        var feed = new AggregateNewsFeed(new[] { a, b });
        var items = await feed.FetchAsync(maxItems: 30);

        // Two sources produce the same two headlines, so dedup leaves two.
        items.Should().HaveCount(2);
    }
}
