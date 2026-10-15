namespace SetGameSolver.Core.Vision;

/// <summary>
/// Eine entzerrte Karte im Hochformat (Symbole übereinander, jedes Symbol liegt waagerecht).
/// Kanäle als float 0..255.
/// </summary>
public sealed class CardPatch
{
    public const int DefaultWidth = 160;
    public const int DefaultHeight = 250;

    public CardPatch(int width, int height)
    {
        Width = width;
        Height = height;
        R = new float[width * height];
        G = new float[width * height];
        B = new float[width * height];
    }

    public int Width { get; }
    public int Height { get; }
    public float[] R { get; }
    public float[] G { get; }
    public float[] B { get; }

    /// <summary>Schneidet die Karte entlang des Vierecks aus und entzerrt sie (2x2-Überabtastung).</summary>
    public static CardPatch Extract(RgbaImage image, Quad quad, int width = DefaultWidth, int height = DefaultHeight)
    {
        var c = quad.Corners;
        // Die kurze Kartenseite soll oben liegen.
        if ((c[1] - c[0]).Length > (c[2] - c[1]).Length)
            c = [c[1], c[2], c[3], c[0]];

        var h = Homography.FromPoints(
            [new(0, 0), new(width, 0), new(width, height), new(0, height)],
            c);

        var patch = new CardPatch(width, height);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            double r = 0, g = 0, b = 0;
            for (var sy = 0; sy < 2; sy++)
            for (var sx = 0; sx < 2; sx++)
            {
                var p = h.Map(new Point2(x + 0.25 + 0.5 * sx, y + 0.25 + 0.5 * sy));
                var s = image.Sample(p.X - 0.5, p.Y - 0.5);
                r += s.R; g += s.G; b += s.B;
            }
            var i = y * width + x;
            patch.R[i] = (float)(r / 4);
            patch.G[i] = (float)(g / 4);
            patch.B[i] = (float)(b / 4);
        }
        return patch;
    }

    /// <summary>
    /// Macht aus einer dunklen Karte (Dunkelmodus einer App) eine helle, ohne die Farben der Symbole zu ändern:
    /// Ein Pixel gilt als Mischung aus Symbolfarbe und schwarzem Papier; der Papieranteil wird durch Weiß ersetzt.
    /// </summary>
    public CardPatch DarkPaperToWhite()
    {
        var margin = (int)Math.Round(0.06 * Width);
        var brightest = 1f;
        for (var y = margin; y < Height - margin; y++)
        for (var x = margin; x < Width - margin; x++)
        {
            var i = y * Width + x;
            brightest = Math.Max(brightest, Math.Max(R[i], Math.Max(G[i], B[i])));
        }
        var result = new CardPatch(Width, Height);
        for (var i = 0; i < R.Length; i++)
        {
            var paper = 255 * Math.Max(0, 1 - Math.Max(R[i], Math.Max(G[i], B[i])) / brightest);
            result.R[i] = Math.Min(255, R[i] + paper);
            result.G[i] = Math.Min(255, G[i] + paper);
            result.B[i] = Math.Min(255, B[i] + paper);
        }
        return result;
    }
}
