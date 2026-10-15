namespace SetGameSolver.Core.Vision;

/// <summary>Ein Bild als RGBA-Bytes (4 Bytes pro Pixel, zeilenweise), wie es ein Canvas liefert.</summary>
public sealed class RgbaImage
{
    public RgbaImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != width * height * 4)
            throw new ArgumentException($"Erwartet {width * height * 4} Bytes, erhalten {pixels.Length}.", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public RgbaImage(int width, int height) : this(width, height, new byte[width * height * 4]) { }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public void Set(int x, int y, byte r, byte g, byte b)
    {
        var i = (y * Width + x) * 4;
        Pixels[i] = r;
        Pixels[i + 1] = g;
        Pixels[i + 2] = b;
        Pixels[i + 3] = 255;
    }

    /// <summary>Bilineare Abtastung, liefert Kanalwerte 0..255.</summary>
    public (double R, double G, double B) Sample(double x, double y)
    {
        x = Math.Clamp(x, 0, Width - 1.001);
        y = Math.Clamp(y, 0, Height - 1.001);
        var x0 = (int)x;
        var y0 = (int)y;
        var fx = x - x0;
        var fy = y - y0;
        var i00 = (y0 * Width + x0) * 4;
        var i10 = i00 + 4;
        var i01 = i00 + Width * 4;
        var i11 = i01 + 4;
        double Lerp(int c) =>
            (Pixels[i00 + c] * (1 - fx) + Pixels[i10 + c] * fx) * (1 - fy) +
            (Pixels[i01 + c] * (1 - fx) + Pixels[i11 + c] * fx) * fy;
        return (Lerp(0), Lerp(1), Lerp(2));
    }

    /// <summary>Verkleinert das Bild per Flächenmittelung, falls die längere Seite größer als <paramref name="maxSide"/> ist.</summary>
    /// <summary>Negativ des Bildes (Alpha bleibt).</summary>
    public RgbaImage Inverted()
    {
        var result = new RgbaImage(Width, Height, (byte[])Pixels.Clone());
        var px = result.Pixels;
        for (var i = 0; i < px.Length; i += 4)
        {
            px[i] = (byte)(255 - px[i]);
            px[i + 1] = (byte)(255 - px[i + 1]);
            px[i + 2] = (byte)(255 - px[i + 2]);
        }
        return result;
    }

    public RgbaImage DownscaleTo(int maxSide)
    {
        var scale = (double)maxSide / Math.Max(Width, Height);
        if (scale >= 1)
            return this;
        var w = Math.Max(1, (int)Math.Round(Width * scale));
        var h = Math.Max(1, (int)Math.Round(Height * scale));
        var result = new RgbaImage(w, h);
        for (var y = 0; y < h; y++)
        {
            var sy0 = (int)(y / scale);
            var sy1 = Math.Min(Height, Math.Max(sy0 + 1, (int)((y + 1) / scale)));
            for (var x = 0; x < w; x++)
            {
                var sx0 = (int)(x / scale);
                var sx1 = Math.Min(Width, Math.Max(sx0 + 1, (int)((x + 1) / scale)));
                int r = 0, g = 0, b = 0, n = 0;
                for (var sy = sy0; sy < sy1; sy++)
                for (var sx = sx0; sx < sx1; sx++)
                {
                    var i = (sy * Width + sx) * 4;
                    r += Pixels[i];
                    g += Pixels[i + 1];
                    b += Pixels[i + 2];
                    n++;
                }
                result.Set(x, y, (byte)(r / n), (byte)(g / n), (byte)(b / n));
            }
        }
        return result;
    }
}
