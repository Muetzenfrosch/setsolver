using System.IO.Compression;
using SetGameSolver.Core.Vision;

namespace SetGameSolver.Core.Tests;

/// <summary>Minimaler PNG-Schreiber, um Testbilder bei Bedarf anzusehen.</summary>
internal static class PngWriter
{
    public static void Write(RgbaImage image, string path)
    {
        using var file = File.Create(path);
        file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        WriteInt(header, 0, image.Width);
        WriteInt(header, 4, image.Height);
        header[8] = 8; header[9] = 6; // 8 Bit, RGBA
        Chunk(file, "IHDR", header);

        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (var y = 0; y < image.Height; y++)
            {
                z.WriteByte(0);
                z.Write(image.Pixels, y * image.Width * 4, image.Width * 4);
            }
        }
        Chunk(file, "IDAT", raw.ToArray());
        Chunk(file, "IEND", []);
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteInt(len, 0, data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = new byte[4];
        WriteInt(crc, 0, (int)Crc(typeBytes.Concat(data)));
        s.Write(crc);
    }

    private static void WriteInt(byte[] b, int o, int v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }

    private static uint Crc(IEnumerable<byte> bytes)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            c ^= b;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }
        return c ^ 0xFFFFFFFFu;
    }
}
