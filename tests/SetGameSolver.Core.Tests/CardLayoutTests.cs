using SetGameSolver.Core.Vision;

namespace SetGameSolver.Core.Tests;

public class CardLayoutTests
{
    /// <summary>
    /// Legt Karten (57 × 89, Mittenabstand 1,2 Kartengrößen) in ein Raster, lässt einzelne Plätze frei,
    /// dreht die Auslage und kippt sie optional nach hinten (hintere Zeilen schmaler, wie schräg fotografiert).
    /// </summary>
    private static (List<Quad> Quads, List<GridCell> Cells) Layout(int rows, int columns, bool landscape = false,
        double rotation = 0, double tilt = 0, params (int Row, int Column)[] missing)
    {
        var (w, h) = landscape ? (178.0, 114.0) : (114.0, 178.0);
        var (stepX, stepY) = (1.2 * w, 1.2 * h);
        var width = columns * stepX;
        var height = rows * stepY;
        // Perspektive: Die obere Kante der Auslage erscheint um den Faktor (1 - tilt) schmaler.
        var inset = width * tilt / 2;
        var perspective = Homography.FromPoints(
            [new(0, 0), new(width, 0), new(width, height), new(0, height)],
            [new(inset, height * tilt / 2), new(width - inset, height * tilt / 2), new(width, height), new(0, height)]);
        var angle = rotation * Math.PI / 180;
        Point2 Place(Point2 p)
        {
            var q = perspective.Map(p);
            var (x, y) = (q.X - width / 2, q.Y - height / 2);
            return new Point2(x * Math.Cos(angle) - y * Math.Sin(angle) + 1000, x * Math.Sin(angle) + y * Math.Cos(angle) + 1000);
        }

        var quads = new List<Quad>();
        var cells = new List<GridCell>();
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < columns; c++)
        {
            if (missing.Contains((r, c))) continue;
            var (x, y) = ((c + 0.5) * stepX, (r + 0.5) * stepY);
            quads.Add(new Quad(
                Place(new(x - w / 2, y - h / 2)), Place(new(x + w / 2, y - h / 2)),
                Place(new(x + w / 2, y + h / 2)), Place(new(x - w / 2, y + h / 2))));
            cells.Add(new GridCell(r, c));
        }
        return (quads, cells);
    }

    [Theory]
    [InlineData(3, 4, false)]
    [InlineData(4, 3, true)]
    [InlineData(5, 3, true)]
    [InlineData(3, 5, false)]
    [InlineData(6, 3, true)]
    [InlineData(3, 6, false)]
    [InlineData(3, 7, false)]
    [InlineData(1, 3, false)]
    [InlineData(3, 1, true)]
    public void Grid_matches_the_layout(int rows, int columns, bool landscape)
    {
        var (quads, cells) = Layout(rows, columns, landscape);

        var grid = CardLayout.Arrange(quads);

        Assert.Equal((rows, columns, landscape), (grid.Rows, grid.Columns, grid.Landscape));
        Assert.Equal(cells, grid.Cells);
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(-12, 0)]
    [InlineData(0, 0.25)]
    [InlineData(6, 0.2)]
    public void Rotated_and_tilted_layouts_keep_their_grid(double rotation, double tilt)
    {
        var (quads, cells) = Layout(4, 3, landscape: true, rotation, tilt);

        var grid = CardLayout.Arrange(quads);

        Assert.Equal((4, 3), (grid.Rows, grid.Columns));
        Assert.Equal(cells, grid.Cells);
    }

    [Fact]
    public void Missing_cards_leave_their_place_free()
    {
        var (quads, cells) = Layout(3, 4, missing: [(0, 3), (1, 1), (2, 0)]);

        var grid = CardLayout.Arrange(quads);

        Assert.Equal((3, 4), (grid.Rows, grid.Columns));
        Assert.Equal(cells, grid.Cells);
    }

    [Fact]
    public void A_missing_middle_column_stays_free()
    {
        var (quads, cells) = Layout(3, 4, rotation: 5, missing: [(0, 1), (1, 1), (2, 1)]);

        var grid = CardLayout.Arrange(quads);

        Assert.Equal((3, 4), (grid.Rows, grid.Columns));
        Assert.Equal(cells, grid.Cells);
    }

    [Fact]
    public void Cards_with_only_diagonal_neighbours_are_placed_by_position()
    {
        var (quads, cells) = Layout(2, 2, missing: [(0, 1), (1, 0)]);

        var grid = CardLayout.Arrange(quads);

        Assert.Equal((2, 2), (grid.Rows, grid.Columns));
        Assert.Equal(cells, grid.Cells);
    }

    [Fact]
    public void Single_card_and_empty_input()
    {
        var (quads, _) = Layout(1, 1);
        Assert.Equal(new CardGrid(1, 1, false, [new GridCell(0, 0)]).Cells, CardLayout.Arrange(quads).Cells);
        Assert.Equal((0, 0), (CardLayout.Arrange([]).Rows, CardLayout.Arrange([]).Columns));
    }

    [Fact]
    public void Scattered_cards_still_get_one_place_each()
    {
        var rnd = new Random(3);
        var quads = Enumerable.Range(0, 10).Select(_ =>
        {
            var (x, y) = (rnd.Next(0, 1500), rnd.Next(0, 1500));
            return new Quad(new(x, y), new(x + 114, y), new(x + 114, y + 178), new(x, y + 178));
        }).ToList();

        var grid = CardLayout.Arrange(quads);

        Assert.Equal(quads.Count, grid.Cells.Distinct().Count());
        Assert.All(grid.Cells, c => Assert.True(c.Row < grid.Rows && c.Column < grid.Columns));
    }
}
