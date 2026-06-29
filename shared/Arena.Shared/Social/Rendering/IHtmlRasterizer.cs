namespace Arena.Shared.Social.Rendering;

/// <summary>
/// Rasterizes a fully-substituted HTML card to a PNG of the given dimensions.
///
/// The default <see cref="SolidColorPngRasterizer"/> is deterministic and dependency-free so the
/// card pipeline (and Gate 4) never depends on a headless browser or the network — honouring §4.4
/// (the social subsystem must not introduce fragile external dependencies into the run path).
/// Production visual fidelity is a drop-in: implement this interface with a headless-Chrome
/// (Playwright/Puppeteer) backend and register it instead. The HTML produced by
/// <see cref="HtmlCardRenderer"/> is identical either way.
/// </summary>
public interface IHtmlRasterizer
{
    Task<byte[]> RasterizeAsync(string html, int width, int height, CancellationToken ct);
}
