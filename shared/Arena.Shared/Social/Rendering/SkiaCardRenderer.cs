using System.Reflection;
using SkiaSharp;

namespace Arena.Shared.Social.Rendering;

/// <summary>
/// Lightweight, browser-free card renderer (SocialPublisher_Spec §8). Draws the <see cref="CardModel"/>
/// text onto the Civersify brand background with SkiaSharp (MIT) — no headless browser, no network,
/// no LLM. This is the production replacement for the placeholder <see cref="SolidColorPngRasterizer"/>,
/// which emitted a featureless solid-colour PNG (blank card).
///
/// The typeface is an <b>embedded</b> DejaVu Sans (Fonts/DejaVuSans.ttf), so rendering is identical on
/// every host and never depends on system fonts being installed — important on minimal Linux App
/// Service images. Bold (the kicker) is synthesised via <see cref="SKPaint.FakeBoldText"/>, so only the
/// regular weight is embedded.
///
/// It honours the same visual language as the embedded HTML templates (navy #0d1b2a background,
/// cyan #4cc9f0 uppercase kicker, white body, muted footer) but does NOT parse HTML/CSS — arbitrary
/// CSS fidelity is the headless-Chrome path's job (see <see cref="IHtmlRasterizer"/>). The body text
/// is word-wrapped and shrink-to-fit so long copy never overflows the card.
/// </summary>
public sealed class SkiaCardRenderer : ICardRenderer
{
    private const string FontResource = "cards.font.regular";
    private const string LogoResource = "cards.logo";

    private static readonly SKColor Background = SKColor.Parse("#0d1b2a");
    private static readonly SKColor BodyColor = SKColors.White;
    private static readonly SKColor FooterColor = SKColor.Parse("#8aa0b5");

    private const int Padding = 64;
    private const float LineSpacing = 1.15f;
    private const float LogoHeight = 32f;       // brand "C" mark height in the footer
    private const float LogoGap = 14f;          // space between the mark and the footer text

    private readonly SKTypeface _typeface;
    private readonly SKBitmap _logo;

    public SkiaCardRenderer()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var fontStream = assembly.GetManifestResourceStream(FontResource)
            ?? throw new InvalidOperationException($"Embedded card font '{FontResource}' not found.");
        _typeface = SKTypeface.FromStream(fontStream)
            ?? throw new InvalidOperationException("Failed to load embedded card font.");

        using var logoStream = assembly.GetManifestResourceStream(LogoResource)
            ?? throw new InvalidOperationException($"Embedded brand mark '{LogoResource}' not found.");
        _logo = SKBitmap.Decode(logoStream)
            ?? throw new InvalidOperationException("Failed to decode embedded brand mark.");
    }

    /// <summary>
    /// Per-template kicker accent (the body/footer/background stay constant). This is the only visual
    /// differentiation between templates — the SkiaCardRenderer is otherwise template-agnostic.
    /// </summary>
    private static SKColor AccentFor(CardTemplate template) => template switch
    {
        CardTemplate.CoalitionHighlight => SKColor.Parse("#5ad19a"), // green — common ground
        CardTemplate.DebateHighlight => SKColor.Parse("#4cc9f0"),    // cyan — the house brand
        CardTemplate.BriefingAnnounce => SKColor.Parse("#f5b14c"),   // amber — briefing
        _ => SKColor.Parse("#a78bfa"),                               // violet — feature (matches the C mark)
    };

    public Task<byte[]> RenderAsync(CardTemplate template, CardModel model, CancellationToken ct)
    {
        using var bitmap = new SKBitmap(model.Width, model.Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(Background);

        var contentWidth = model.Width - (2 * Padding);
        using var kicker = MakePaint(34f, AccentFor(template), bold: true);
        using var footer = MakePaint(26f, FooterColor, bold: false);

        var kickerText = (model.Headline ?? string.Empty).ToUpperInvariant();
        var footerText = model.Footer ?? string.Empty;

        // The footer row carries the brand mark, so it is at least as tall as the logo.
        var footerRowHeight = Math.Max(LogoHeight, MeasureHeight(footerText, footer, contentWidth));

        // Top-down layout: kicker, then body fills the gap above the footer row.
        var bodyTop = Padding + MeasureHeight(kickerText, kicker, contentWidth) + 24f;
        var footerTop = model.Height - Padding - footerRowHeight;
        var bodyAvailable = footerTop - bodyTop - 24f;

        using var body = FitBody(model.Body ?? string.Empty, contentWidth, bodyAvailable);

        DrawBlock(canvas, kickerText, kicker, Padding, contentWidth);
        DrawBlock(canvas, model.Body ?? string.Empty, body, bodyTop, contentWidth);
        DrawFooter(canvas, footerText, footer, footerTop, footerRowHeight);

        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Task.FromResult(data.ToArray());
    }

    /// <summary>Shrinks the body font (64→24px) until the wrapped text fits the available height.</summary>
    private SKPaint FitBody(string text, float width, float availableHeight)
    {
        for (var size = 64f; size > 24f; size -= 4f)
        {
            var paint = MakePaint(size, BodyColor, bold: false);
            if (MeasureHeight(text, paint, width) <= availableHeight) return paint;
            paint.Dispose();
        }
        return MakePaint(24f, BodyColor, bold: false);
    }

    private static float MeasureHeight(string text, SKPaint paint, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return WrapText(text, paint, maxWidth).Count * paint.FontSpacing * LineSpacing;
    }

    private static void DrawBlock(SKCanvas canvas, string text, SKPaint paint, float top, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return;
        var lineHeight = paint.FontSpacing * LineSpacing;
        var y = top - paint.FontMetrics.Ascent; // first baseline (Ascent is negative)
        foreach (var line in WrapText(text, paint, maxWidth))
        {
            canvas.DrawText(line, Padding, y, paint);
            y += lineHeight;
        }
    }

    /// <summary>
    /// Draws the footer row: the brand "C" mark at the left, followed by the footer text, both
    /// vertically centred within <paramref name="rowHeight"/>.
    /// </summary>
    private void DrawFooter(SKCanvas canvas, string text, SKPaint paint, float top, float rowHeight)
    {
        var logoWidth = LogoHeight * _logo.Width / _logo.Height;
        var logoRect = SKRect.Create(Padding, top + (rowHeight - LogoHeight) / 2f, logoWidth, LogoHeight);
        using (var img = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High })
            canvas.DrawBitmap(_logo, logoRect, img);

        if (string.IsNullOrEmpty(text)) return;
        // Centre the single text line on the logo's vertical midpoint.
        var midY = top + rowHeight / 2f;
        var baseline = midY - (paint.FontMetrics.Ascent + paint.FontMetrics.Descent) / 2f;
        canvas.DrawText(text, Padding + logoWidth + LogoGap, baseline, paint);
    }

    /// <summary>Greedy word-wrap. A single word wider than the line is left to overflow (rare for post copy).</summary>
    private static List<string> WrapText(string text, SKPaint paint, float maxWidth)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var current = string.Empty;
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length == 0 || paint.MeasureText(candidate) <= maxWidth)
                {
                    current = candidate;
                }
                else
                {
                    lines.Add(current);
                    current = word;
                }
            }
            lines.Add(current);
        }
        return lines;
    }

    private SKPaint MakePaint(float size, SKColor color, bool bold) => new()
    {
        Typeface = _typeface,
        TextSize = size,
        Color = color,
        IsAntialias = true,
        FakeBoldText = bold,
        SubpixelText = true,
    };
}
