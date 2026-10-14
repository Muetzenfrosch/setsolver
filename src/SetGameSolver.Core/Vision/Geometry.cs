namespace SetGameSolver.Core.Vision;

public readonly record struct Point2(double X, double Y)
{
    public static Point2 operator +(Point2 a, Point2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Point2 operator -(Point2 a, Point2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Point2 operator *(Point2 a, double f) => new(a.X * f, a.Y * f);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public static double Dot(Point2 a, Point2 b) => a.X * b.X + a.Y * b.Y;
    public static double Cross(Point2 a, Point2 b) => a.X * b.Y - a.Y * b.X;
}

/// <summary>Ein Viereck mit Eckpunkten in Bildkoordinaten (im Uhrzeigersinn).</summary>
public sealed record Quad(Point2 A, Point2 B, Point2 C, Point2 D)
{
    public Point2[] Corners => [A, B, C, D];
    public Point2 Center => new((A.X + B.X + C.X + D.X) / 4, (A.Y + B.Y + C.Y + D.Y) / 4);
    public double Area => Math.Abs(Geometry.PolygonArea(Corners));
}

public static class Geometry
{
    public static double PolygonArea(IReadOnlyList<Point2> poly)
    {
        double sum = 0;
        for (var i = 0; i < poly.Count; i++)
            sum += Point2.Cross(poly[i], poly[(i + 1) % poly.Count]);
        return sum / 2;
    }

    /// <summary>Konvexe Hülle (Andrew's Monotone Chain), Ergebnis gegen den Uhrzeigersinn im mathematischen Sinn.</summary>
    public static List<Point2> ConvexHull(IEnumerable<Point2> points)
    {
        var pts = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (pts.Count < 3)
            return pts;
        var hull = new Point2[pts.Count * 2];
        var k = 0;
        foreach (var p in pts)
        {
            while (k >= 2 && Point2.Cross(hull[k - 1] - hull[k - 2], p - hull[k - 2]) <= 0) k--;
            hull[k++] = p;
        }
        for (int i = pts.Count - 2, t = k + 1; i >= 0; i--)
        {
            var p = pts[i];
            while (k >= t && Point2.Cross(hull[k - 1] - hull[k - 2], p - hull[k - 2]) <= 0) k--;
            hull[k++] = p;
        }
        return hull.Take(k - 1).ToList();
    }

    /// <summary>Kleinstes umschließendes Rechteck einer konvexen Hülle (Rotating Calipers über die Hüllkanten).</summary>
    public static Quad MinAreaRect(IReadOnlyList<Point2> hull)
    {
        if (hull.Count < 3)
            throw new ArgumentException("Mindestens drei Punkte nötig.", nameof(hull));

        var bestArea = double.MaxValue;
        Quad? best = null;
        for (var i = 0; i < hull.Count; i++)
        {
            var edge = hull[(i + 1) % hull.Count] - hull[i];
            var len = edge.Length;
            if (len < 1e-9) continue;
            var u = edge * (1 / len);
            var v = new Point2(-u.Y, u.X);
            double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
            foreach (var p in hull)
            {
                var pu = Point2.Dot(p, u);
                var pv = Point2.Dot(p, v);
                minU = Math.Min(minU, pu); maxU = Math.Max(maxU, pu);
                minV = Math.Min(minV, pv); maxV = Math.Max(maxV, pv);
            }
            var area = (maxU - minU) * (maxV - minV);
            if (area < bestArea)
            {
                bestArea = area;
                Point2 P(double a, double b) => u * a + v * b;
                best = new Quad(P(minU, minV), P(maxU, minV), P(maxU, maxV), P(minU, maxV));
            }
        }
        return best!;
    }

    /// <summary>
    /// Größtes Viereck mit Ecken auf einer konvexen Hülle (gleicher Umlaufsinn wie die Hülle). Schräg
    /// fotografierte Karten sind Trapeze; dieses Viereck beschreibt sie besser als ein Rechteck.
    /// Für jede Diagonale i–j wandern die besten Gegenecken k und l nur vorwärts, daher O(n²).
    /// </summary>
    public static Quad LargestInscribedQuad(IReadOnlyList<Point2> hull)
    {
        var n = hull.Count;
        if (n < 4)
            throw new ArgumentException("Mindestens vier Punkte nötig.", nameof(hull));
        Point2 P(int index) => hull[index % n];
        double Triangle(int a, int b, int c) => Math.Abs(Point2.Cross(P(b) - P(a), P(c) - P(a))) / 2;

        var best = -1.0;
        int bi = 0, bk = 1, bj = 2, bl = 3;
        for (var i = 0; i < n; i++)
        {
            var k = i + 1;
            var l = i + 3;
            for (var j = i + 2; j <= i + n - 2; j++)
            {
                while (k + 1 < j && Triangle(i, k + 1, j) >= Triangle(i, k, j)) k++;
                if (l <= j) l = j + 1;
                while (l + 1 < i + n && Triangle(j, l + 1, i) >= Triangle(j, l, i)) l++;
                var area = Triangle(i, k, j) + Triangle(j, l, i);
                if (area > best)
                {
                    best = area;
                    (bi, bk, bj, bl) = (i, k, j, l);
                }
            }
        }
        return new Quad(P(bi), P(bk), P(bj), P(bl));
    }

    /// <summary>Gerade durch Punktwolke (Hauptkomponente). Liefert Punkt auf der Geraden und Richtung.</summary>
    public static (Point2 Origin, Point2 Direction) FitLine(IReadOnlyList<Point2> points)
    {
        double mx = 0, my = 0;
        foreach (var p in points) { mx += p.X; my += p.Y; }
        mx /= points.Count; my /= points.Count;
        double sxx = 0, syy = 0, sxy = 0;
        foreach (var p in points)
        {
            var dx = p.X - mx;
            var dy = p.Y - my;
            sxx += dx * dx; syy += dy * dy; sxy += dx * dy;
        }
        var angle = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        return (new Point2(mx, my), new Point2(Math.Cos(angle), Math.Sin(angle)));
    }

    public static Point2? Intersect((Point2 Origin, Point2 Direction) l1, (Point2 Origin, Point2 Direction) l2)
    {
        var denom = Point2.Cross(l1.Direction, l2.Direction);
        if (Math.Abs(denom) < 1e-9) return null;
        var t = Point2.Cross(l2.Origin - l1.Origin, l2.Direction) / denom;
        return l1.Origin + l1.Direction * t;
    }
}

/// <summary>Projektive Abbildung (3x3-Matrix, h22 = 1).</summary>
public sealed class Homography
{
    private readonly double[] _m;

    private Homography(double[] m) => _m = m;

    public Point2 Map(Point2 p)
    {
        var w = _m[6] * p.X + _m[7] * p.Y + _m[8];
        return new Point2((_m[0] * p.X + _m[1] * p.Y + _m[2]) / w, (_m[3] * p.X + _m[4] * p.Y + _m[5]) / w);
    }

    /// <summary>Berechnet die Abbildung, die die vier Punkte <paramref name="from"/> auf <paramref name="to"/> abbildet.</summary>
    public static Homography FromPoints(IReadOnlyList<Point2> from, IReadOnlyList<Point2> to)
    {
        if (from.Count != 4 || to.Count != 4)
            throw new ArgumentException("Genau vier Punktpaare nötig.");
        var a = new double[8, 9];
        for (var i = 0; i < 4; i++)
        {
            double x = from[i].X, y = from[i].Y, u = to[i].X, v = to[i].Y;
            double[] r1 = [x, y, 1, 0, 0, 0, -u * x, -u * y, u];
            double[] r2 = [0, 0, 0, x, y, 1, -v * x, -v * y, v];
            for (var j = 0; j < 9; j++) { a[2 * i, j] = r1[j]; a[2 * i + 1, j] = r2[j]; }
        }
        // Gauß-Elimination mit Pivotsuche
        for (var col = 0; col < 8; col++)
        {
            var pivot = col;
            for (var r = col + 1; r < 8; r++)
                if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col])) pivot = r;
            if (Math.Abs(a[pivot, col]) < 1e-12)
                throw new InvalidOperationException("Punkte liegen degeneriert.");
            for (var j = 0; j < 9; j++) (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
            for (var r = 0; r < 8; r++)
            {
                if (r == col) continue;
                var f = a[r, col] / a[col, col];
                for (var j = col; j < 9; j++) a[r, j] -= f * a[col, j];
            }
        }
        var m = new double[9];
        for (var i = 0; i < 8; i++) m[i] = a[i, 8] / a[i, i];
        m[8] = 1;
        return new Homography(m);
    }

    public Homography Inverse()
    {
        var m = _m;
        var inv = new[]
        {
            m[4] * m[8] - m[5] * m[7], m[2] * m[7] - m[1] * m[8], m[1] * m[5] - m[2] * m[4],
            m[5] * m[6] - m[3] * m[8], m[0] * m[8] - m[2] * m[6], m[2] * m[3] - m[0] * m[5],
            m[3] * m[7] - m[4] * m[6], m[1] * m[6] - m[0] * m[7], m[0] * m[4] - m[1] * m[3],
        };
        var s = inv[8];
        for (var i = 0; i < 9; i++) inv[i] /= s;
        return new Homography(inv);
    }
}
