using SetGameSolver.Core.Model;

namespace SetGameSolver.Core.Vision;

/// <summary>Gemessene Merkmale einer Karte; hilfreich zum Nachjustieren der Schwellwerte mit echten Fotos.</summary>
public sealed record CardFeatures(
    int SymbolCount,
    double Hue,
    double Solidity,
    double Extent,
    double FillRatio,
    double Texture);

public sealed record ClassifiedCard(Card Card, CardFeatures Features, bool Uncertain);

/// <summary>Bestimmt Anzahl, Farbe, Füllung und Form einer entzerrten Karte.</summary>
public static class CardClassifier
{
    // Schwellwerte, eingestellt an echten Fotos (tests/.../Photos) und synthetischen Karten.
    // Gemessen an Kunstlicht-Fotos: Füllung leer ≤ 0,11, gestreift 0,17–0,39 (unscharf bis 0,63), voll ≥ 0,69;
    // Konvexität Welle ≤ 0,87, sonst ≥ 0,93; Flächenanteil Raute ≤ 0,54, Oval ≥ 0,75.
    private const double InkThreshold = 0.28;
    private const double MinSymbolArea = 0.02;
    private const double SquiggleMaxSolidity = 0.885;
    private const double MinSolidity = 0.7, MinExtent = 0.4;
    private const double DiamondMaxExtent = 0.64;
    private const double SolidMinScore = 0.45; // Füllung minus 0,8 × Streifenmuster
    private const double OpenMaxFill = 0.14;
    private const double GreenMinHue = 70, PurpleMinHue = 200, RedMinHue = 330;

    // Lage der Symbolmitten (Abstand zur Kartenmitte relativ zur Kartenhöhe): bei zwei Symbolen ≈ 0,14,
    // bei dreien 0 und ≈ 0,28. Damit lässt sich die Anzahl auch schätzen, wenn ein Symbol in einer
    // Spiegelung verschwindet.
    private const double TwoSymbolOffset = 0.07;
    private const double ThreeSymbolOffset = 0.19;

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

        // So zerklüftete Formen gibt es auf Set-Karten nicht: vermutlich keine Karte oder stark verdeckt.
        if (solidity < MinSolidity || extent < MinExtent)
            return null;
        // Rauten füllen ihr Rechteck nur zur Hälfte; Wellen sind deutlich nicht konvex, bei Unschärfe hilft
        // zusätzlich der geringere Flächenanteil gegenüber dem Oval.
        var shape = extent < DiamondMaxExtent && solidity > 0.92 ? Shape.Diamond
            : solidity < SquiggleMaxSolidity || (solidity < 0.93 && extent < 0.81) ? Shape.Squiggle
            : extent < DiamondMaxExtent ? Shape.Diamond
            : Shape.Oval;
        uncertain |= Math.Abs(solidity - SquiggleMaxSolidity) < 0.02
            || (shape != Shape.Squiggle && Math.Abs(extent - DiamondMaxExtent) < 0.06);

        // Füllung: mittlere Tintenstärke im Inneren relativ zur Stärke des Umrisses.
        var symbolMask = new BinaryMask(w, h);
        foreach (var s in symbols)
            foreach (var i in s.Pixels)
                symbolMask.Data[i] = true;
        var interior = symbolMask.Erode(Math.Max(3, w / 40));
        var silhouetteInk = symbols.SelectMany(s => s.Pixels).Select(i => ink[i]).OrderBy(v => v).ToArray();
        var strong = silhouetteInk[(int)(0.9 * (silhouetteInk.Length - 1))];
        double interiorSum = 0, interiorSq = 0;
        var interiorCount = 0;
        for (var i = 0; i < interior.Data.Length; i++)
        {
            if (!interior.Data[i]) continue;
            interiorSum += ink[i];
            interiorSq += ink[i] * ink[i];
            interiorCount++;
        }
        var interiorMean = interiorCount == 0 ? 0 : interiorSum / interiorCount;
        var fill = interiorMean / Math.Max(0.05, strong);
        // Streifen erzeugen im Inneren ein Auf und Ab der Tintenstärke, volle Flächen sind gleichmäßig.
        var texture = interiorCount == 0 ? 0
            : Math.Sqrt(Math.Max(0, interiorSq / interiorCount - interiorMean * interiorMean)) / Math.Max(0.05, strong);
        var solidScore = fill - 0.8 * texture;
        var shading = solidScore > SolidMinScore ? Shading.Solid
            : fill < OpenMaxFill ? Shading.Open
            : Shading.Striped;
        uncertain |= Math.Abs(solidScore - SolidMinScore) < 0.07 || Math.Abs(fill - OpenMaxFill) < 0.03;

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
        var color = hue is >= GreenMinHue and < PurpleMinHue ? Color.Green
            : hue is >= PurpleMinHue and < RedMinHue ? Color.Purple
            : Color.Red;
        uncertain |= new[] { GreenMinHue, PurpleMinHue, RedMinHue }.Any(t => Math.Abs(hue - t) < 10);

        // Anzahl: gezählte Symbole, ergänzt um die Lage der gefundenen Symbole. Ein einzelnes Symbol
        // abseits der Mitte gehört zu einer Karte mit zwei oder drei Symbolen; zwei Symbole mit einer
        // Lücke dazwischen zu einer Karte mit dreien.
        var count = symbols.Count;
        var implied = count;
        if (count == 1)
        {
            var offset = Math.Abs(symbols[0].Centroid.Y - h / 2.0) / h;
            implied = offset > ThreeSymbolOffset ? 3 : offset > TwoSymbolOffset ? 2 : 1;
        }
        else if (count == 2)
        {
            var gap = Math.Abs(symbols[0].Centroid.Y - symbols[1].Centroid.Y);
            var symbolHeight = symbols.Average(s => s.BoundsHeight);
            if (gap > 2 * symbolHeight)
                implied = 3;
        }
        if (implied > count)
        {
            uncertain = true;
            count = implied;
        }
        var number = (Number)(count - 1);
        var features = new CardFeatures(count, hue, solidity, extent, fill, texture);
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
