using System.Reflection;
using Arena.Shared.News;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Arena.Shared.Tests;

public class NewsSourceFactoryTests
{
    /// <summary>Serves the plain-RSS fixture for every request.</summary>
    private sealed class FixtureHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = asm.GetManifestResourceNames()
                .First(n => n.EndsWith("sample-rss.xml", StringComparison.OrdinalIgnoreCase));
            using var stream = asm.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return new HttpClient(StubHttpMessageHandler.FromBody(reader.ReadToEnd(), "application/xml"));
        }
    }

    private static NewsSourceFactory BuildFactory()
    {
        var http = new FixtureHttpClientFactory();
        var loggers = NullLoggerFactory.Instance;
        return new NewsSourceFactory(
            new INewsSourceBuilder[] { new RssSourceBuilder(http, loggers), new GoogleNewsSourceBuilder(http, loggers) },
            loggers);
    }

    [Fact]
    public void TryCreate_BuildsRssAndGoogleNewsByKind()
    {
        var factory = BuildFactory();

        var rss = factory.TryCreate("NPR", new NewsSourceConfig { Kind = "Rss", Url = "https://feeds.npr.org/1001/rss.xml" });
        rss.Should().BeOfType<RssNewsSource>().Which.Name.Should().Be("NPR");

        var gn = factory.TryCreate("Google News", new NewsSourceConfig { Kind = "GoogleNews", Feed = GoogleNewsFeedKind.Top });
        gn.Should().BeOfType<GoogleNewsSource>().Which.Name.Should().Be("Google News");
    }

    [Fact]
    public void TryCreate_KindIsCaseInsensitive()
    {
        var factory = BuildFactory();
        var src = factory.TryCreate("X", new NewsSourceConfig { Kind = "googlenews" });
        src.Should().BeOfType<GoogleNewsSource>();
    }

    [Fact]
    public void TryCreate_InvalidEntries_ReturnNullWithoutThrowing()
    {
        var factory = BuildFactory();

        factory.TryCreate("unknown", new NewsSourceConfig { Kind = "Carrier-Pigeon" }).Should().BeNull();
        factory.TryCreate("disabled", new NewsSourceConfig { Kind = "Rss", Url = "https://ok.example/rss", Enabled = false }).Should().BeNull();
        factory.TryCreate("bad-url", new NewsSourceConfig { Kind = "Rss", Url = "not a url" }).Should().BeNull();
        factory.TryCreate("no-topic", new NewsSourceConfig { Kind = "GoogleNews", Feed = GoogleNewsFeedKind.Topic }).Should().BeNull();
    }

    [Fact]
    public async Task CreateFeed_AggregatesValidSources_AndSkipsInvalid()
    {
        var factory = BuildFactory();
        var feed = factory.CreateFeed(new Dictionary<string, NewsSourceConfig>
        {
            ["Sample"] = new() { Kind = "Rss", Url = "https://example.com/rss.xml" },
            ["broken"] = new() { Kind = "Rss" },              // no Url — skipped
            ["mystery"] = new() { Kind = "Carrier-Pigeon" },  // unknown Kind — skipped
        });

        var items = await feed.FetchAsync(maxItems: 30);
        items.Should().NotBeEmpty();
        items.Should().OnlyContain(i => i.Source == "Sample");
    }
}
