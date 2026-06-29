using System.Buffers.Binary;
using Arena.Shared.Social;
using Arena.Shared.Social.Rendering;
using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace Arena.UnitTests.Social;

/// <summary>
/// Gate 4 (SocialPublisher_Spec §9, Phase 4): card rendering. Deterministic template substitution
/// (golden HTML snapshot, no leftover tokens) + a non-empty PNG of the expected dimensions.
/// </summary>
public class Gate4_CardRendererTests
{
    [Fact]
    public void BuildHtml_feature_matches_golden_and_has_no_tokens()
    {
        var model = new CardModel("Hello & Welcome", "Body <b>text</b>", "civersify.app", 1200, 675);
        var html = HtmlCardRenderer.BuildHtml(CardTemplate.FeaturePost, model);

        const string golden =
            "<!DOCTYPE html>\n" +
            "<html lang=\"en\">\n" +
            "<head><meta charset=\"utf-8\"><style>body{margin:0;font-family:Arial,sans-serif;background:#0d1b2a;color:#fff}main{box-sizing:border-box;padding:64px;height:100%}.kicker{color:#4cc9f0;text-transform:uppercase}</style></head>\n" +
            "<body data-card=\"feature\" style=\"width:1200px;height:675px\">\n" +
            "<main><p class=\"kicker\">Civersify</p><h1>Hello &amp; Welcome</h1><p class=\"body\">Body &lt;b&gt;text&lt;/b&gt;</p><footer>civersify.app</footer></main>\n" +
            "</body>\n" +
            "</html>";

        html.Should().Be(golden);
        html.Should().NotContain("{{").And.NotContain("}}");
    }

    [Theory]
    [InlineData(CardTemplate.FeaturePost)]
    [InlineData(CardTemplate.DebateHighlight)]
    [InlineData(CardTemplate.CoalitionHighlight)]
    [InlineData(CardTemplate.BriefingAnnounce)]
    public void BuildHtml_every_template_leaves_no_tokens(CardTemplate template)
    {
        var html = HtmlCardRenderer.BuildHtml(template, new CardModel("H", "B", "F"));
        html.Should().NotContain("{{").And.NotContain("}}");
    }

    [Theory]
    [InlineData(1200, 675)]
    [InlineData(1080, 1080)]
    public async Task RenderAsync_produces_nonempty_png_of_expected_dimensions(int width, int height)
    {
        var renderer = new HtmlCardRenderer(new SolidColorPngRasterizer());
        var model = new CardModel("Headline", "Body copy", "Civersify", width, height);

        var png = await renderer.RenderAsync(CardTemplate.DebateHighlight, model, default);

        png.Should().NotBeNullOrEmpty();
        // PNG signature.
        png.Take(8).Should().Equal(137, 80, 78, 71, 13, 10, 26, 10);
        // IHDR width/height live at byte offsets 16 and 20 (big-endian).
        BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)).Should().Be((uint)width);
        BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)).Should().Be((uint)height);
    }

    [Theory]
    [InlineData(1200, 675)]
    [InlineData(1080, 1080)]
    public async Task SkiaRenderer_produces_png_of_expected_dimensions(int width, int height)
    {
        var png = await new SkiaCardRenderer().RenderAsync(
            CardTemplate.CoalitionHighlight,
            new CardModel("Common Ground", "Body copy", "Civersify", width, height), default);

        png.Should().NotBeNullOrEmpty();
        png.Take(8).Should().Equal(137, 80, 78, 71, 13, 10, 26, 10);
        using var bmp = SKBitmap.Decode(png);
        bmp.Width.Should().Be(width);
        bmp.Height.Should().Be(height);
    }

    /// <summary>
    /// Regression guard for the "blank card" bug: the old SolidColorPngRasterizer ignored the HTML and
    /// emitted a single-colour fill. A real render must paint multiple distinct colours (text on the
    /// brand background), so the pixel set is never a single value.
    /// </summary>
    [Fact]
    public async Task SkiaRenderer_draws_content_not_a_solid_fill()
    {
        var png = await new SkiaCardRenderer().RenderAsync(
            CardTemplate.DebateHighlight,
            new CardModel("Debate Highlight", "Should cities cap rideshare vehicles?", "Civersify"), default);

        using var bmp = SKBitmap.Decode(png);
        var distinct = new HashSet<uint>();
        for (var y = 0; y < bmp.Height; y += 4)
            for (var x = 0; x < bmp.Width; x += 4)
                distinct.Add((uint)bmp.GetPixel(x, y));

        distinct.Count.Should().BeGreaterThan(1, "a rendered card must contain text, not a single flat colour");
    }
}
