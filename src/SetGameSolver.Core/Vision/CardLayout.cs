namespace SetGameSolver.Core.Vision;

/// <summary>Platz einer Karte im Raster der Auslage (Zeile und Spalte ab 0, oben links beginnend).</summary>
public readonly record struct GridCell(int Row, int Column);

/// <summary>Raster der Auslage wie auf dem Foto: Zeilen, Spalten, Lage der Karten und ein Platz je Karte.</summary>
/// <param name="Landscape">Die Karten liegen überwiegend quer.</param>
/// <param name="Cells">Platz jeder Karte, in derselben Reihenfolge wie die übergebenen Umrisse.</param>
public sealed record CardGrid(int Rows, int Columns, bool Landscape, IReadOnlyList<GridCell> Cells)
{
    public static readonly CardGrid Empty = new(0, 0, false, []);
}

/// <summary>
/// Leitet aus den Kartenumrissen das Raster der Auslage ab (z. B. 3 × 4, 4 × 3 oder 5 × 3). Plätze ohne erkannte
/// Karte bleiben frei. Ist zwischen zwei Zeilen oder Spalten etwa doppelt so viel Platz wie sonst, fehlt dort
/// eine ganze Zeile bzw. Spalte, die dann ebenfalls frei bleibt.
/// </summary>
public static class CardLayout
{
    private const double MaxLinkAngle = 30;    // Nachbar rechts/unten: höchstens so schräg (Grad, in Kartengrößen)
    private const double MaxLinkShift = 0.6;   // und höchstens so weit seitlich versetzt (Kartengrößen)
    private const double MaxLinkDistance = 4;  // und höchstens so weit entfernt (Kartengrößen)
    private const double DefaultStep = 1.25;   // üblicher Mittenabstand, wenn es keine Nachbarn gibt

    public static CardGrid Arrange(IReadOnlyList<Quad> quads)
    {
        var n = quads.Count;
        if (n == 0)
            return CardGrid.Empty;

        // Drehung der Auslage: Richtung aller Kartenkanten modulo 90° (über den vierfachen Winkel gemittelt).
        double sx = 0, sy = 0;
        foreach (var q in quads)
        {
            var (e1, e2) = Edges(q);
            foreach (var e in new[] { e1, e2 })
            {
                var a = 4 * Math.Atan2(e.Y, e.X);
                sx += Math.Cos(a) * e.Length;
                sy += Math.Sin(a) * e.Length;
            }
        }
        var theta = Math.Atan2(sy, sx) / 4;
        var cos = Math.Cos(-theta);
        var sin = Math.Sin(-theta);
        Point2 Rotate(Point2 p) => new(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);

        // Kartenmitten und -größen im ausgerichteten Bild: die eher waagerechte Kante ist die Breite.
        var centers = new Point2[n];
        var widths = new double[n];
        var heights = new double[n];
        for (var i = 0; i < n; i++)
        {
            var (e1, e2) = Edges(quads[i]);
            var r1 = Rotate(e1);
            var r2 = Rotate(e2);
            var (across, along) = Math.Abs(r1.X) >= Math.Abs(r2.X) ? (r1, r2) : (r2, r1);
            centers[i] = Rotate(quads[i].Center);
            widths[i] = across.Length;
            heights[i] = along.Length;
        }
        var landscape = Enumerable.Range(0, n).Count(i => widths[i] > heights[i]) * 2 > n;

        var cells = Lattice(centers, widths, heights) ?? RowsFromLeft(centers, heights);
        var minRow = cells.Min(c => c.Row);
        var minColumn = cells.Min(c => c.Column);
        for (var i = 0; i < n; i++)
            cells[i] = new GridCell(cells[i].Row - minRow, cells[i].Column - minColumn);
        return new CardGrid(cells.Max(c => c.Row) + 1, cells.Max(c => c.Column) + 1, landscape, cells);
    }

    /// <summary>
    /// Verbindet jede Karte mit ihrem nächsten Nachbarn rechts und unten (höchstens 30° und eine gute halbe
    /// Kartengröße seitlich versetzt) und zählt entlang dieser Verbindungen Spalten und Zeilen ab. Eine Verbindung
    /// über etwa doppelten Abstand überspringt einen freien Platz. Schräge Zeilen und perspektivisch zulaufende
    /// Spalten stören so nicht. Kürzere Verbindungen zählen zuerst; eine, die dem schon Gefundenen widerspricht,
    /// wird übergangen. Liefert null, wenn sich kein widerspruchsfreies Raster ergibt.
    /// </summary>
    private static GridCell[]? Lattice(Point2[] centers, double[] widths, double[] heights)
    {
        var n = centers.Length;
        var tan = Math.Tan(MaxLinkAngle * Math.PI / 180);
        (double Along, double Across) Offset(int i, int j, bool horizontal)
        {
            var dx = (centers[j].X - centers[i].X) / ((widths[i] + widths[j]) / 2);
            var dy = (centers[j].Y - centers[i].Y) / ((heights[i] + heights[j]) / 2);
            return horizontal ? (dx, dy) : (dy, dx);
        }
        List<(int From, int To, double Along, double Distance)> Links(bool horizontal)
        {
            var links = new List<(int, int, double, double)>();
            for (var i = 0; i < n; i++)
            {
                var best = -1;
                var bestDistance = double.MaxValue;
                for (var j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    var (along, across) = Offset(i, j, horizontal);
                    if (along <= 0 || along > MaxLinkDistance || Math.Abs(across) > Math.Min(tan * along, MaxLinkShift))
                        continue;
                    var distance = Math.Sqrt(along * along + across * across);
                    if (distance < bestDistance) { best = j; bestDistance = distance; }
                }
                if (best >= 0)
                    links.Add((i, best, Offset(i, best, horizontal).Along, bestDistance));
            }
            return links;
        }

        var right = Links(horizontal: true);
        var down = Links(horizontal: false);
        var stepX = UsualStep(right.Select(l => l.Along));
        var stepY = UsualStep(down.Select(l => l.Along));
        int Steps(double along, double step) => Math.Max(1, (int)Math.Round(along / step));
        var links = right.Select(l => (l.From, l.To, Rows: 0, Columns: Steps(l.Along, stepX), l.Distance))
            .Concat(down.Select(l => (l.From, l.To, Rows: Steps(l.Along, stepY), Columns: 0, l.Distance)))
            .OrderBy(l => l.Distance);

        // Gruppen schrittweise zusammenführen; jede Gruppe hält die Plätze ihrer Karten relativ zueinander.
        var cells = new GridCell[n];
        var group = Enumerable.Range(0, n).ToArray();
        var members = Enumerable.Range(0, n).Select(i => new List<int> { i }).ToArray();
        foreach (var (from, to, rows, columns, _) in links)
        {
            var (a, b) = (group[from], group[to]);
            if (a == b) continue;
            var shiftRow = cells[from].Row + rows - cells[to].Row;
            var shiftColumn = cells[from].Column + columns - cells[to].Column;
            var taken = members[a].Select(i => cells[i]).ToHashSet();
            if (members[b].Any(i => taken.Contains(new GridCell(cells[i].Row + shiftRow, cells[i].Column + shiftColumn))))
                continue;
            foreach (var i in members[b])
            {
                cells[i] = new GridCell(cells[i].Row + shiftRow, cells[i].Column + shiftColumn);
                group[i] = a;
            }
            members[a].AddRange(members[b]);
            members[b].Clear();
        }

        // Getrennte Gruppen (z. B. nur schräg benachbarte Karten) nach ihrer Lage im Bild zur größten Gruppe einordnen.
        var groups = members.Where(m => m.Count > 0).OrderByDescending(m => m.Count).ToList();
        if (groups.Count > 1)
        {
            var width = Median(widths) * stepX;
            var height = Median(heights) * stepY;
            double RowOffset(List<int> g) => Median(g.Select(i => centers[i].Y / height - cells[i].Row));
            double ColumnOffset(List<int> g) => Median(g.Select(i => centers[i].X / width - cells[i].Column));
            var (row0, column0) = (RowOffset(groups[0]), ColumnOffset(groups[0]));
            foreach (var g in groups.Skip(1))
            {
                var rowShift = (int)Math.Round(RowOffset(g) - row0);
                var columnShift = (int)Math.Round(ColumnOffset(g) - column0);
                foreach (var i in g)
                    cells[i] = new GridCell(cells[i].Row + rowShift, cells[i].Column + columnShift);
            }
        }
        return cells.Distinct().Count() == n ? cells : null;
    }

    /// <summary>Üblicher Abstand benachbarter Karten (in Kartengrößen); größere Abstände überspringen freie Plätze.</summary>
    private static double UsualStep(IEnumerable<double> distances)
    {
        var all = distances.ToArray();
        if (all.Length == 0)
            return DefaultStep;
        var shortest = all.Min();
        return Median(all.Where(d => d < 1.5 * shortest));
    }

    /// <summary>Ersatz, wenn die Karten kein sauberes Raster bilden: Zeilen nach Höhe, jede Zeile von links gefüllt.</summary>
    private static GridCell[] RowsFromLeft(Point2[] centers, double[] heights)
    {
        var n = centers.Length;
        var order = Enumerable.Range(0, n).OrderBy(i => centers[i].Y).ToList();
        var rows = new List<List<int>> { new() { order[0] } };
        for (var k = 1; k < n; k++)
        {
            var (a, b) = (order[k - 1], order[k]);
            if (centers[b].Y - centers[a].Y > 0.5 * (heights[a] + heights[b]) / 2)
                rows.Add([]);
            rows[^1].Add(b);
        }
        var cells = new GridCell[n];
        for (var r = 0; r < rows.Count; r++)
        {
            var k = 0;
            foreach (var i in rows[r].OrderBy(i => centers[i].X))
                cells[i] = new GridCell(r, k++);
        }
        return cells;
    }

    /// <summary>Gemittelte gegenüberliegende Kanten AB/DC und BC/AD.</summary>
    private static (Point2, Point2) Edges(Quad q) =>
        (((q.B - q.A) + (q.C - q.D)) * 0.5, ((q.C - q.B) + (q.D - q.A)) * 0.5);

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }
}
