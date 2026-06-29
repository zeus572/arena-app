using System.Net;
using System.Reflection;
using System.Collections.Concurrent;

namespace Arena.Shared.Social.Rendering;

/// <summary>
/// Server-side card renderer (SocialPublisher_Spec §8). Fills a per-ContentType HTML template with
/// <see cref="CardModel"/> data via deterministic <c>{{token}}</c> substitution, then rasterizes to
/// PNG via the injected <see cref="IHtmlRasterizer"/>. No network, no LLM — the card is decorative
/// reinforcement of already-validated copy, never a second source of truth.
/// </summary>
public sealed class HtmlCardRenderer : ICardRenderer
{
    private readonly IHtmlRasterizer _rasterizer;
    private static readonly ConcurrentDictionary<CardTemplate, string> Cache = new();

    public HtmlCardRenderer(IHtmlRasterizer rasterizer) => _rasterizer = rasterizer;

    public async Task<byte[]> RenderAsync(CardTemplate template, CardModel model, CancellationToken ct)
    {
        var html = BuildHtml(template, model);
        return await _rasterizer.RasterizeAsync(html, model.Width, model.Height, ct);
    }

    /// <summary>Substitutes all tokens and returns the final HTML. Throws if any <c>{{ }}</c> remains.</summary>
    internal static string BuildHtml(CardTemplate template, CardModel model)
    {
        var raw = LoadTemplate(template).Replace("\r\n", "\n");

        var html = raw
            .Replace("{{width}}", model.Width.ToString())
            .Replace("{{height}}", model.Height.ToString())
            .Replace("{{headline}}", WebUtility.HtmlEncode(model.Headline))
            .Replace("{{body}}", WebUtility.HtmlEncode(model.Body))
            .Replace("{{footer}}", WebUtility.HtmlEncode(model.Footer))
            .Trim();

        if (html.Contains("{{") || html.Contains("}}"))
            throw new InvalidOperationException($"Card template '{template}' has unsubstituted tokens.");

        return html;
    }

    private static string LoadTemplate(CardTemplate template) =>
        Cache.GetOrAdd(template, t =>
        {
            var name = "cards." + t switch
            {
                CardTemplate.FeaturePost => "feature.html",
                CardTemplate.DebateHighlight => "debate.html",
                CardTemplate.CoalitionHighlight => "coalition.html",
                CardTemplate.BriefingAnnounce => "briefing.html",
                _ => throw new ArgumentOutOfRangeException(nameof(template)),
            };
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded card template '{name}' not found.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
}
