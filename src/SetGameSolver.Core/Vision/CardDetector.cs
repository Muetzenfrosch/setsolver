namespace SetGameSolver.Core.Vision;

/// <summary>
/// Findet Karten im Foto: Karten sind helle, kaum gesättigte Rechtecke vor einem dunkleren Hintergrund.
/// </summary>
public static class CardDetector
{
    private const double MinAspect = 1.25;
    private const double MaxAspect = 2.0;
    private const double MinPaperScore = 0.2;   // absolute Untergrenze (0..1) für Kartenpapier
    private const double PaperRatio = 0.45;     // Anteil der örtlichen Helligkeits-Obergrenze
    private const double EdgeThreshold = 0.35;  // Sobel-Betrag relativ zur örtlichen Obergrenze

    /// <summary>Kartenmaske zur Fehlersuche.</summary>
    public static BinaryMask DebugMask(RgbaImage image) => CardMask(image);

    public static List<Quad> FindCards(RgbaImage image)
    {
        var w = image.Width;
        var h = image.Height;
        var mask = CardMask(image);

        // Leicht erodieren, damit sich knapp berührende Karten trennen.
        var erosion = Math.Max(2, Math.Min(w, h) / 300);
        var eroded = mask.Erode(erosion);

        var minArea = (int)(0.002 * w * h);
        var candidates = new List<(Quad Quad, double Area)>();
        foreach (var comp in eroded.Components(minArea))
            Evaluate(comp, eroded.Width, erosion, minArea, candidates, depth: 0);

        if (candidates.Count == 0)
            return [];

        // Kleine Ausreißer (Lichtreflexe, Papierschnipsel) relativ zur typischen Kartengröße verwerfen.
        var median = candidates.Select(c => c.Area).OrderBy(a => a).ElementAt(candidates.Count / 2);
        // Karten, die über den Bildrand hinausragen, lassen sich nicht sicher erkennen.
        var tolerance = 0.01 * Math.Max(w, h);
        bool Inside(Quad q) => q.Corners.All(p =>
            p.X >= -tolerance && p.Y >= -tolerance && p.X <= w + tolerance && p.Y <= h + tolerance);
        return candidates
            .Where(c => c.Area > 0.4 * median && Inside(c.Quad))
            .Select(c => c.Quad)
            .OrderBy(q => q.Center.Y)
            .ThenBy(q => q.Center.X)
            .ToList();
    }

    /// <summary>
    /// Prüft, ob eine Fläche eine Karte ist. Große Flächen, die den Test nicht bestehen (z. B. Karte plus
    /// angrenzende helle Fliese), werden stärker erodiert und ihre Teile erneut geprüft.
    /// </summary>
    private static void Evaluate(Component comp, int imageWidth, int erosion, int minArea,
        List<(Quad Quad, double Area)> candidates, int depth)
    {
        var outer = comp.OuterPoints();
        var hull = Geometry.ConvexHull(outer);
        if (hull.Count >= 4)
        {
            var rect = Geometry.MinAreaRect(hull);
            var hullArea = Math.Abs(Geometry.PolygonArea(hull));
            var side1 = (rect.B - rect.A).Length;
            var side2 = (rect.C - rect.B).Length;
            var aspect = Math.Max(side1, side2) / Math.Max(1e-9, Math.Min(side1, side2));
            var rectangular = hullArea / rect.Area >= 0.9;
            var solid = comp.Area / hullArea >= 0.3; // Symbole sind Löcher, ein bloßer Rahmen ist keine Karte
            if (rectangular && solid && aspect is >= MinAspect and <= MaxAspect)
            {
                var quad = RefineQuad(outer, rect, erosion);
                candidates.Add((quad, quad.Area));
                return;
            }
        }

        if (depth >= 3 || comp.Area < 3 * minArea)
            return;

        // Teilmaske ausschneiden, kräftiger erodieren und die Teile einzeln prüfen.
        var extra = Math.Max(2, erosion);
        var pad = extra + 1;
        var sub = new BinaryMask(comp.BoundsWidth + 2 * pad, comp.BoundsHeight + 2 * pad);
        foreach (var i in comp.Pixels)
            sub[i % imageWidth - comp.MinX + pad, i / imageWidth - comp.MinY + pad] = true;
        foreach (var part in sub.Erode(extra).Components(minArea))
        {
            var shifted = part.Translate(comp.MinX - pad, comp.MinY - pad, imageWidth);
            Evaluate(shifted, imageWidth, erosion + extra, minArea, candidates, depth + 1);
        }
    }

    /// <summary>
    /// Pixel, die nach Kartenpapier aussehen. Zuerst Weißabgleich auf die hellsten Bildteile
    /// (bei Kunstlicht wirken Karten sonst gelblich), dann zählt Helligkeit abzüglich Farbsättigung.
    /// </summary>
    internal static BinaryMask CardMask(RgbaImage image)
    {
        var w = image.Width;
        var h = image.Height;
        var px = image.Pixels;
        var n = w * h;

        // Referenzweiß = Mittelwert der hellsten 10 % der Pixel.
        var sumHist = new int[766];
        for (var i = 0; i < n; i++)
            sumHist[px[i * 4] + px[i * 4 + 1] + px[i * 4 + 2]]++;
        var limit = 765;
        for (int acc = 0; limit > 0 && acc + sumHist[limit] < n / 10; limit--)
            acc += sumHist[limit];
        double rs = 0, gs = 0, bs = 0;
        var count = 0;
        for (var i = 0; i < n; i++)
        {
            var o = i * 4;
            if (px[o] + px[o + 1] + px[o + 2] < limit) continue;
            rs += px[o]; gs += px[o + 1]; bs += px[o + 2];
            count++;
        }
        var gr = 255 * count / Math.Max(1, rs);
        var gg = 255 * count / Math.Max(1, gs);
        var gb = 255 * count / Math.Max(1, bs);

        // Helligkeit abzüglich Sättigung, 0..1.
        var score = new float[n];
        var lum = new float[n];
        for (var i = 0; i < n; i++)
        {
            var o = i * 4;
            var r = Math.Min(255, px[o] * gr);
            var g = Math.Min(255, px[o + 1] * gg);
            var b = Math.Min(255, px[o + 2] * gb);
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            lum[i] = (float)((r + g + b) / (3 * 255));
            score[i] = (float)Math.Max(0, ((r + g + b) / 3 - 1.5 * (max - min)) / 255);
        }

        // Ungleichmäßige Beleuchtung ausgleichen: Kartenpapier ist örtlich das Hellste,
        // daher Bezug auf die örtliche Obergrenze (Maximum in einer Umgebung von etwa Kartengröße).
        var envelope = LocalEnvelope(score, w, h, Math.Max(4, Math.Min(w, h) / 40), 2);

        // Kanten (Sobel auf der Helligkeit) trennen Karten von gleich hellen Nachbarflächen.
        var mask = new BinaryMask(w, h);
        for (var y = 1; y < h - 1; y++)
        for (var x = 1; x < w - 1; x++)
        {
            var i = y * w + x;
            var env = envelope[i];
            if (score[i] < MinPaperScore || score[i] < PaperRatio * env) continue;
            var gx = lum[i - w + 1] + 2 * lum[i + 1] + lum[i + w + 1] - lum[i - w - 1] - 2 * lum[i - 1] - lum[i + w - 1];
            var gy = lum[i + w - 1] + 2 * lum[i + w] + lum[i + w + 1] - lum[i - w - 1] - 2 * lum[i - w] - lum[i - w + 1];
            if (Math.Sqrt(gx * gx + gy * gy) > EdgeThreshold * env) continue;
            mask.Data[i] = true;
        }
        return mask;
    }

    /// <summary>
    /// Örtliche Obergrenze: 90-%-Wert je Block, dann Maximum über benachbarte Blöcke, bilinear interpoliert.
    /// </summary>
    internal static float[] LocalEnvelope(float[] values, int w, int h, int block, int radiusBlocks)
    {
        var bw = (w + block - 1) / block;
        var bh = (h + block - 1) / block;
        var blocks = new float[bw * bh];
        var buffer = new List<float>(block * block);
        for (var by = 0; by < bh; by++)
        for (var bx = 0; bx < bw; bx++)
        {
            buffer.Clear();
            for (var y = by * block; y < Math.Min(h, (by + 1) * block); y++)
            for (var x = bx * block; x < Math.Min(w, (bx + 1) * block); x++)
                buffer.Add(values[y * w + x]);
            buffer.Sort();
            blocks[by * bw + bx] = buffer[(int)(0.9 * (buffer.Count - 1))];
        }

        var dilated = new float[bw * bh];
        for (var by = 0; by < bh; by++)
        for (var bx = 0; bx < bw; bx++)
        {
            var m = 0f;
            for (var dy = -radiusBlocks; dy <= radiusBlocks; dy++)
            for (var dx = -radiusBlocks; dx <= radiusBlocks; dx++)
            {
                int x = bx + dx, y = by + dy;
                if (x < 0 || y < 0 || x >= bw || y >= bh || dx * dx + dy * dy > radiusBlocks * radiusBlocks) continue;
                m = Math.Max(m, blocks[y * bw + x]);
            }
            dilated[by * bw + bx] = m;
        }

        var result = new float[w * h];
        for (var y = 0; y < h; y++)
        {
            var fy = Math.Clamp((y + 0.5) / block - 0.5, 0, bh - 1);
            var y0 = (int)fy;
            var y1 = Math.Min(bh - 1, y0 + 1);
            var ty = (float)(fy - y0);
            for (var x = 0; x < w; x++)
            {
                var fx = Math.Clamp((x + 0.5) / block - 0.5, 0, bw - 1);
                var x0 = (int)fx;
                var x1 = Math.Min(bw - 1, x0 + 1);
                var tx = (float)(fx - x0);
                result[y * w + x] =
                    (dilated[y0 * bw + x0] * (1 - tx) + dilated[y0 * bw + x1] * tx) * (1 - ty) +
                    (dilated[y1 * bw + x0] * (1 - tx) + dilated[y1 * bw + x1] * tx) * ty;
            }
        }
        return result;
    }

    /// <summary>
    /// Passt an jede Rechteckseite eine Gerade durch die Randpunkte an und schneidet benachbarte Geraden.
    /// So werden perspektivisch verzerrte Karten genauer erfasst als mit dem Rechteck allein.
    /// </summary>
    private static Quad RefineQuad(List<Point2> outer, Quad rect, double expand)
    {
        var corners = rect.Corners;
        var center = rect.Center;
        var shortSide = Math.Min((corners[1] - corners[0]).Length, (corners[2] - corners[1]).Length);
        var lines = new (Point2 Origin, Point2 Direction)[4];
        for (var k = 0; k < 4; k++)
        {
            var a = corners[k];
            var b = corners[(k + 1) % 4];
            var len = (b - a).Length;
            var dir = (b - a) * (1 / len);
            var normal = new Point2(-dir.Y, dir.X);
            if (Point2.Dot(normal, a - center) < 0) normal = normal * -1;

            var sidePoints = outer.Where(p =>
            {
                var t = Point2.Dot(p - a, dir);
                var d = Math.Abs(Point2.Dot(p - a, normal));
                return t > 0.15 * len && t < 0.85 * len && d < 0.08 * shortSide;
            }).ToList();

            var line = sidePoints.Count >= 6 ? Geometry.FitLine(sidePoints) : (Origin: a, Direction: dir);
            lines[k] = (line.Origin + normal * expand, line.Direction);
        }

        var refined = new Point2[4];
        for (var k = 0; k < 4; k++)
        {
            var p = Geometry.Intersect(lines[(k + 3) % 4], lines[k]);
            if (p is null) return Expand(rect, expand);
            refined[k] = p.Value;
        }
        // Die verfeinerten Ecken dürfen nur wenig von den Rechteckecken abweichen, sonst lieber das Rechteck.
        var expanded = Expand(rect, expand);
        var expandedCorners = expanded.Corners;
        for (var k = 0; k < 4; k++)
        {
            if ((refined[k] - expandedCorners[k]).Length > 0.15 * shortSide)
                return expanded;
        }
        return new Quad(refined[0], refined[1], refined[2], refined[3]);
    }

    private static Quad Expand(Quad q, double by)
    {
        var c = q.Center;
        Point2 E(Point2 p)
        {
            var d = p - c;
            return p + d * (by * Math.Sqrt(2) / d.Length);
        }
        return new Quad(E(q.A), E(q.B), E(q.C), E(q.D));
    }
}
