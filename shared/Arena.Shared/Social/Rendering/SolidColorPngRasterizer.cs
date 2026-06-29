using System.Buffers.Binary;
using System.IO.Compression;

namespace Arena.Shared.Social.Rendering;

/// <summary>
/// Deterministic, dependency-free PNG backend. Emits a valid solid-colour PNG of exactly the
/// requested dimensions (24-bit RGB). No browser, no network, no LLM — see <see cref="IHtmlRasterizer"/>
/// for why the default is offline. Swap for a headless-Chrome rasterizer for production fidelity.
/// </summary>
public sealed class SolidColorPngRasterizer : IHtmlRasterizer
{
    // Civersify brand background (#0d1b2a).
    private static readonly byte[] Rgb = { 0x0d, 0x1b, 0x2a };

    public Task<byte[]> RasterizeAsync(string html, int width, int height, CancellationToken ct)
        => Task.FromResult(Encode(width, height));

    public static byte[] Encode(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Card dimensions must be positive.");

        using var ms = new MemoryStream();
        ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }); // PNG signature

        // IHDR: width, height, bitDepth=8, colorType=2 (RGB), no compression/filter/interlace.
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 8; ihdr[9] = 2; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        WriteChunk(ms, "IHDR", ihdr);

        // Raw scanlines: each row = filter byte (0) + width RGB pixels.
        var stride = width * 3;
        var raw = new byte[height * (stride + 1)];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * (stride + 1);
            raw[rowStart] = 0; // filter: none
            for (var x = 0; x < width; x++)
            {
                var p = rowStart + 1 + x * 3;
                raw[p] = Rgb[0]; raw[p + 1] = Rgb[1]; raw[p + 2] = Rgb[2];
            }
        }

        // IDAT: zlib-compressed scanlines (ZLibStream emits the zlib header + Adler-32 PNG requires).
        using var idat = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
            z.Write(raw, 0, raw.Length);
        WriteChunk(ms, "IDAT", idat.ToArray());

        WriteChunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
        s.Write(len);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        var crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        s.Write(crcBytes);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in type) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
