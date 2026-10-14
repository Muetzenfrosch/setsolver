using SetGameSolver.Core.Model;

namespace SetGameSolver.Core.Vision;

/// <summary>Gemessene Merkmale einer Karte; hilfreich zum Nachjustieren der Schwellwerte mit echten Fotos.</summary>
public sealed record CardFeatures(
    int SymbolCount,
    double Hue,
    double Solidity,
    double Extent,
    double FillRatio);

public sealed record ClassifiedCard(Card Card, CardFeatures Features, bool Uncertain);

/// <summary>Bestimmt Anzahl, Farbe, Füllung und Form einer entzerrten Karte.</summary>
public static class CardClassifier
{
    // Schwellwerte, ermittelt an synthetischen Karten; mit echten Fotos ggf. nachjustieren.
    private const double InkThreshold = 0.28;
    private const double MinSymbolArea = 0.02;
    private const double SquiggleMaxSolidity = 0.9;
    private const double DiamondMaxExtent = 0.7;
    private const double SolidMinFill = 0.65;
    private const double OpenMaxFill = 0.2;

    public static ClassifiedCard? Classify(CardPatch patch)
    {
        var w = patch.Width;
        var h = patch.Height;
        var margin = (int)Math.Round(0.06 * w);

        // Weißabgleich: Das Kartenpapier ist der häufigste Farbton, daher Median je Kanal.
        var wr = Math.Max(30, Median(patch.R, w, h, margin));
        var wg = Math.Max(30, Median(patch.G, w, h, margin));
        var wb = Math.Max(30, Median(patch.B, w, h, margin));

        // Tintenstärke: 0 = Papierweiß, 1 = Kanal komplett dunkel. Jede der drei Set-Farben hat einen dunklen Kanal.
        var ink = new float[w * h];
        var inkMask = new BinaryMask(w, h);
        for (var y = margin; y < h - margin; y++)
        for (var x = margin; x < w - margin; x++)
        {
            var i = y * w + x;
            var s = 1 - Math.Min(patch.R[i] / wr, Math.Min(patch.G[i] / wg, patch.B[i] / wb));
            ink[i] = (float)Math.Clamp(s, 0, 1);
            inkMask.Data[i] = s > InkThreshold;
        }

        // Umrisse schließen, Innenflächen füllen: ergibt die Silhouette jedes Symbols.
        var silhouette = inkMask.Dilate(2).FillHoles().Erode(2);
        var symbols = silhouette.Components((int)(MinSymbolArea * w * h))
            .Where(c => c.BoundsWidth > 0.35 * w)
            .OrderByDescending(c => c.Area)
            .ToList();
        if (symbols.Count == 0)
            return null;

        var uncertain = symbols.Count > 3;
        if (symbols.Count > 3)
            symbols = symbols.Take(3).ToList();

        // Gleich große Symbole erwartet; deutlich kleinere sind vermutlich Störungen.
        var largest = symbols[0].Area;
        if (symbols.Any(s => s.Area < 0.5 * largest))
        {
            uncertain = true;
            symbols = symbols.Where(s => s.Area >= 0.5 * largest).ToList();
        }

        // Form
        double solidity = 0, extent = 0;
        foreach (var s in symbols)
        {
            var hullArea = Math.Abs(Geometry.PolygonArea(s.Hull()));
            solidity += Math.Min(1, s.Area / Math.Max(1, hullArea));
            extent += (double)s.Area / (s.BoundsWidth * s.BoundsHeight);
        }
        solidity /= symbols.Count;
        extent /= symbols.Count;
        var shape = solidity < SquiggleMaxSolidity ? Shape.Squiggle
            : extent < DiamondMaxExtent ? Shape.Diamond
            : Shape.Oval;
        uncertain |= Math.Abs(solidity - SquiggleMaxSolidity) < 0.02
            || (shape != Shape.Squiggle && Math.Abs(extent - DiamondMaxExtent) < 0.04);

        // Füllung: mittlere Tintenstärke im Inneren relativ zur Stärke des Umrisses.
        var symbolMask = new BinaryMask(w, h);
        foreach (var s in symbols)
            foreach (var i in s.Pixels)
                symbolMask.Data[i] = true;
        var interior = symbolMask.Erode(Math.Max(3, w / 40));
        var silhouetteInk = symbols.SelectMany(s => s.Pixels).Select(i => ink[i]).OrderBy(v => v).ToArray();
        var strong = silhouetteInk[(int)(0.9 * (silhouetteInk.Length - 1))];
        double interiorSum = 0;
        var interiorCount = 0;
        for (var i = 0; i < interior.Data.Length; i++)
        {
            if (!interior.Data[i]) continue;
            interiorSum += ink[i];
            interiorCount++;
        }
        var fill = interiorCount == 0 ? 0 : interiorSum / interiorCount / Math.Max(0.05, strong);
        var shading = fill > SolidMinFill ? Shading.Solid
            : fill < OpenMaxFill ? Shading.Open
            : Shading.Striped;
        uncertain |= Math.Abs(fill - SolidMinFill) < 0.05 || Math.Abs(fill - OpenMaxFill) < 0.04;

        // Farbe: Farbton des Mittelwerts der kräftig gefärbten Pixel (nach Weißabgleich).
        double sr = 0, sg = 0, sb = 0;
        var n = 0;
        foreach (var s in symbols)
        foreach (var i in s.Pixels)
        {
            if (ink[i] < 0.6 * strong) continue;
            sr += patch.R[i] / wr; sg += patch.G[i] / wg; sb += patch.B[i] / wb;
            n++;
        }
        var hue = n == 0 ? 0 : Hue(sr / n, sg / n, sb / n);
        var color = hue is >= 70 and < 200 ? Color.Green
            : hue is >= 200 and < 325 ? Color.Purple
            : Color.Red;
        uncertain |= new[] { 70.0, 200, 325 }.Any(t => Math.Abs(hue - t) < 10);

        var number = (Number)(symbols.Count - 1);
        var features = new CardFeatures(symbols.Count, hue, solidity, extent, fill);
        return new ClassifiedCard(new Card(number, color, shading, shape), features, uncertain);
    }

    private static float Median(float[] channel, int w, int h, int margin)
    {
        var hist = new int[256];
        var count = 0;
        for (var y = margin; y < h - margin; y++)
        for (var x = margin; x < w - margin; x++)
        {
            hist[Math.Clamp((int)channel[y * w + x], 0, 255)]++;
            count++;
        }
        var acc = 0;
        for (var v = 0; v < 256; v++)
        {
            acc += hist[v];
            if (acc * 2 >= count) return v;
        }
        return 255;
    }

    /// <summary>Farbton in Grad (0..360).</summary>
    internal static double Hue(double r, double g, double b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;
        if (d < 1e-9) return 0;
        double hue;
        if (max == r) hue = 60 * ((g - b) / d % 6);
        else if (max == g) hue = 60 * ((b - r) / d + 2);
        else hue = 60 * ((r - g) / d + 4);
        return hue < 0 ? hue + 360 : hue;
    }
}
