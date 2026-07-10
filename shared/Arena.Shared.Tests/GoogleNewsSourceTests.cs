using System.Reflection;
using Arena.Shared.News;
using FluentAssertions;
using Xunit;

namespace Arena.Shared.Tests;

public class GoogleNewsSourceTests
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

    private static GoogleNewsSource FixtureSource(string name = "Google News")
    {
        var xml = LoadFixture("sample-google-news.xml");
        var http = new HttpClient(StubHttpMessageHandler.FromBody(xml, "application/xml"));
        return new GoogleNewsSource(http, name, new NewsSourceConfig { Kind = NewsSourceKinds.GoogleNews });
    }

    [Fact]
    public void BuildFeedUrl_Top()
    {
        var url = GoogleNewsSource.BuildFeedUrl(new NewsSourceConfig { Feed = GoogleNewsFeedKind.Top });
        url.ToString().Should().Be("https://news.google.com/rss?hl=en-US&gl=US&ceid=US:en");
    }

    [Fact]
    public void BuildFeedUrl_Topic_UppercasesTopicId()
    {
        var url = GoogleNewsSource.BuildFeedUrl(new NewsSourceConfig { Feed = GoogleNewsFeedKind.Topic, Topic = "politics" });
        url.ToString().Should().Be("https://news.google.com/rss/headlines/section/topic/POLITICS?hl=en-US&gl=US&ceid=US:en");
    }

    [Fact]
    public void BuildFeedUrl_Geo_EscapesLocation()
    {
        var url = GoogleNewsSource.BuildFeedUrl(new NewsSourceConfig { Feed = GoogleNewsFeedKind.Geo, Location = "Washington State" });
        url.AbsoluteUri.Should().Be("https://news.google.com/rss/headlines/section/geo/Washington%20State?hl=en-US&gl=US&ceid=US:en");
    }

    [Fact]
    public void BuildFeedUrl_Search_EscapesQuery()
    {
        var url = GoogleNewsSource.BuildFeedUrl(new NewsSourceConfig { Feed = GoogleNewsFeedKind.Search, Query = "ballot measure" });
        url.AbsoluteUri.Should().Be("https://news.google.com/rss/search?q=ballot%20measure&hl=en-US&gl=US&ceid=US:en");
    }

    [Theory]
    [InlineData(GoogleNewsFeedKind.Topic)]
    [InlineData(GoogleNewsFeedKind.Geo)]
    [InlineData(GoogleNewsFeedKind.Search)]
    public void BuildFeedUrl_MissingRequiredField_Throws(GoogleNewsFeedKind feed)
    {
        var act = () => GoogleNewsSource.BuildFeedUrl(new NewsSourceConfig { Feed = feed });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task FetchAsync_StripsPublisherSuffix_AndSetsPublisher()
    {
        var items = await FixtureSource().FetchAsync();

        var voting = items.Should().ContainSingle(i => i.Headline.Contains("voting rights")).Subject;
        voting.Headline.Should().Be("Senate passes sweeping voting rights overhaul");
        voting.Publisher.Should().Be("NPR");
        voting.Source.Should().Be("Google News");

        // En-dash variant strips too.
        var disaster = items.Should().ContainSingle(i => i.Headline.Contains("disaster funding")).Subject;
        disaster.Headline.Should().Be("Governors clash over federal disaster funding rules");
        disaster.Publisher.Should().Be("The Washington Post");
    }

    [Fact]
    public async Task FetchAsync_KeepsHyphensInsideHeadline()
    {
        var items = await FixtureSource().FetchAsync();

        var debt = items.Should().ContainSingle(i => i.Headline.Contains("Debt-ceiling")).Subject;
        debt.Headline.Should().Be("Debt-ceiling standoff enters third week");
        debt.Publisher.Should().Be("Reuters");
    }

    [Fact]
    public async Task FetchAsync_MissingSourceElement_LeavesTitle_PublisherNull()
    {
        var items = await FixtureSource().FetchAsync();

        var ai = items.Should().ContainSingle(i => i.Headline.Contains("AI regulation")).Subject;
        ai.Headline.Should().Be("State legislatures debate AI regulation frameworks");
        ai.Publisher.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_SkipsItemsBelowMinTitleLengthAfterStrip()
    {
        var items = await FixtureSource().FetchAsync();

        // "Big news today - Chronicle" passes the raw length check but the
        // stripped headline ("Big news today") is below the 15-char minimum.
        items.Should().NotContain(i => i.Headline.Contains("Big news"));
        items.Should().HaveCount(4);
    }

    [Fact]
    public async Task FetchAsync_DropsClusterDescription_AndUsesGuidAsExternalId()
    {
        var items = await FixtureSource().FetchAsync();

        items.Should().OnlyContain(i => i.Summary == null, "Google News descriptions are HTML link clusters, not summaries");
        var voting = items.Single(i => i.Headline.Contains("voting rights"));
        voting.ExternalId.Should().Be("CBMiqwFBVV95cUxQVm90aW5nUmlnaHRzT3ZlcmhhdWxHdWlkVmFsdWU");
        voting.Url.Should().StartWith("https://news.google.com/rss/articles/");
    }

    [Fact]
    public async Task FetchAsync_NetworkFailure_ReturnsEmpty_AndDoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("boom"));
        var src = new GoogleNewsSource(new HttpClient(handler), "Google News", new NewsSourceConfig());

        var items = await src.FetchAsync();
        items.Should().BeEmpty();
    }
}
