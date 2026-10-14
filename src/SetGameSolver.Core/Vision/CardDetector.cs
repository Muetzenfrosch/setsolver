namespace SetGameSolver.Core.Vision;

/// <summary>
/// Findet Karten im Foto: Karten sind helle, kaum gesättigte Rechtecke vor einem dunkleren Hintergrund.
/// </summary>
public static class CardDetector
{
    private const double MinAspect = 1.25;
    private const double MaxAspect = 2.0;

    public static List<Quad> FindCards(RgbaImage image)
    {
        var w = image.Width;
        var h = image.Height;
        var px = image.Pixels;

        // "Weißheit" = kleinster Farbkanal: hoch nur bei hellen, ungesättigten Pixeln (Kartenpapier).
        var whiteness = new byte[w * h];
        var histogram = new int[256];
        for (var i = 0; i < whiteness.Length; i++)
        {
            var o = i * 4;
            var v = Math.Min(px[o], Math.Min(px[o + 1], px[o + 2]));
            whiteness[i] = v;
            histogram[v]++;
        }
        var threshold = Otsu(histogram);

        var mask = new BinaryMask(w, h);
        for (var i = 0; i < whiteness.Length; i++)
            mask.Data[i] = whiteness[i] > threshold;

        // Leicht erodieren, damit sich knapp berührende Karten trennen.
        var erosion = Math.Max(2, Math.Min(w, h) / 300);
        var eroded = mask.Erode(erosion);

        var minArea = (int)(0.002 * w * h);
        var candidates = new List<(Quad Quad, double Area)>();
        foreach (var comp in eroded.Components(minArea))
        {
            var outer = comp.OuterPoints();
            var hull = Geometry.ConvexHull(outer);
            if (hull.Count < 4) continue;
            var rect = Geometry.MinAreaRect(hull);
            var rectArea = rect.Area;
            var hullArea = Math.Abs(Geometry.PolygonArea(hull));
            var side1 = (rect.B - rect.A).Length;
            var side2 = (rect.C - rect.B).Length;
            var aspect = Math.Max(side1, side2) / Math.Max(1e-9, Math.Min(side1, side2));

            if (hullArea / rectArea < 0.85) continue;       // nicht rechteckig
            if (comp.Area / hullArea < 0.5) continue;       // zu löchrig
            if (aspect < MinAspect || aspect > MaxAspect) continue;

            var quad = RefineQuad(outer, rect, erosion);
            candidates.Add((quad, quad.Area));
        }

        if (candidates.Count == 0)
            return [];

        // Kleine Ausreißer (Lichtreflexe, Papierschnipsel) relativ zur typischen Kartengröße verwerfen.
        var median = candidates.Select(c => c.Area).OrderBy(a => a).ElementAt(candidates.Count / 2);
        return candidates
            .Where(c => c.Area > 0.4 * median)
            .Select(c => c.Quad)
            .OrderBy(q => q.Center.Y)
            .ThenBy(q => q.Center.X)
            .ToList();
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
        var quad = new Quad(refined[0], refined[1], refined[2], refined[3]);
        var expanded = Expand(rect, expand);
        var ratio = quad.Area / expanded.Area;
        return ratio is > 0.85 and < 1.15 ? quad : expanded;
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

    /// <summary>Otsu-Schwellwert: maximiert die Varianz zwischen den beiden Klassen.</summary>
    internal static int Otsu(int[] histogram)
    {
        long total = 0, sum = 0;
        for (var i = 0; i < histogram.Length; i++) { total += histogram[i]; sum += (long)i * histogram[i]; }
        long wB = 0, sumB = 0;
        double best = -1;
        var threshold = 0;
        for (var t = 0; t < histogram.Length; t++)
        {
            wB += histogram[t];
            if (wB == 0) continue;
            var wF = total - wB;
            if (wF == 0) break;
            sumB += (long)t * histogram[t];
            var mB = (double)sumB / wB;
            var mF = (double)(sum - sumB) / wF;
            var between = (double)wB * wF * (mB - mF) * (mB - mF);
            if (between > best) { best = between; threshold = t; }
        }
        return threshold;
    }
}
