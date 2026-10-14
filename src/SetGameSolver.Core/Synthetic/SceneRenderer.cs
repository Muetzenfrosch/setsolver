using SetGameSolver.Core.Model;
using SetGameSolver.Core.Vision;

namespace SetGameSolver.Core.Synthetic;

/// <summary>Eine in die Szene gelegte Karte mit ihrer Lage im Bild.</summary>
public sealed record PlacedCard(Card Card, Quad Quad);

public sealed record Scene(RgbaImage Image, IReadOnlyList<PlacedCard> Cards);

/// <summary>
/// Erzeugt ein künstliches "Foto" einer Kartenauslage: leicht gedrehte, perspektivisch verzerrte Karten
/// auf einem Tisch, mit Lichtverlauf, Farbstich und Rauschen.
/// </summary>
public static class SceneRenderer
{
    public static Scene Render(IReadOnlyList<Card> cards, int seed, int width = 1200, int height = 900,
        int columns = 4, bool landscapeCards = false, double noise = 6)
    {
        var rnd = new Random(seed);
        var rows = (cards.Count + columns - 1) / columns;

        // Zellgröße so wählen, dass die Karten mit Abstand ins Bild passen.
        var cardW = landscapeCards ? CardArt.Height : CardArt.Width;
        var cardH = landscapeCards ? CardArt.Width : CardArt.Height;
        var scale = Math.Min(width * 0.9 / (columns * cardW * 1.18), height * 0.9 / (rows * cardH * 1.18));
        var cellW = cardW * 1.18 * scale;
        var cellH = cardH * 1.18 * scale;
        var originX = (width - columns * cellW) / 2;
        var originY = (height - rows * cellH) / 2;

        var placed = new List<(PlacedCard Card, Homography ImageToCard)>();
        for (var k = 0; k < cards.Count; k++)
        {
            var cx = originX + (k % columns + 0.5) * cellW + (rnd.NextDouble() - 0.5) * 0.06 * cellW;
            var cy = originY + (k / columns + 0.5) * cellH + (rnd.NextDouble() - 0.5) * 0.06 * cellH;
            var angle = (rnd.NextDouble() - 0.5) * 0.25 + (landscapeCards ? Math.PI / 2 : 0) + (rnd.Next(2) * Math.PI);
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            Point2 Place(double x, double y)
            {
                // Kartenkoordinate (mm) -> Bild, plus etwas Perspektive je Ecke.
                var px = (x - CardArt.Width / 2) * scale;
                var py = (y - CardArt.Height / 2) * scale;
                var jitter = 0.025 * CardArt.Width * scale;
                return new Point2(
                    cx + px * cos - py * sin + (rnd.NextDouble() - 0.5) * jitter,
                    cy + px * sin + py * cos + (rnd.NextDouble() - 0.5) * jitter);
            }
            Point2[] cardCorners = [new(0, 0), new(CardArt.Width, 0), new(CardArt.Width, CardArt.Height), new(0, CardArt.Height)];
            var imageCorners = cardCorners.Select(p => Place(p.X, p.Y)).ToArray();
            var toImage = Homography.FromPoints(cardCorners, imageCorners);
            placed.Add((new PlacedCard(cards[k], new Quad(imageCorners[0], imageCorners[1], imageCorners[2], imageCorners[3])), toImage.Inverse()));
        }

        var image = new RgbaImage(width, height);
        var tint = (R: 1.0, G: 0.97 + rnd.NextDouble() * 0.03, B: 0.88 + rnd.NextDouble() * 0.08);
        var lightAngle = rnd.NextDouble() * Math.PI * 2;
        (double R, double G, double B) table = (60 + rnd.Next(40), 45 + rnd.Next(30), 30 + rnd.Next(25));

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            double r = 0, g = 0, b = 0;
            for (var s = 0; s < 4; s++)
            {
                var sx = x + 0.25 + 0.5 * (s % 2);
                var sy = y + 0.25 + 0.5 * (s / 2);
                var c = Shade(sx, sy, placed, table);
                r += c.R; g += c.G; b += c.B;
            }
            var nx = (x / (double)width - 0.5) * Math.Cos(lightAngle) + (y / (double)height - 0.5) * Math.Sin(lightAngle);
            var light = 0.95 + 0.25 * nx;
            byte Channel(double v, double t) =>
                (byte)Math.Clamp(v / 4 * light * t + (rnd.NextDouble() - 0.5) * 2 * noise, 0, 255);
            image.Set(x, y, Channel(r, tint.R), Channel(g, tint.G), Channel(b, tint.B));
        }

        return new Scene(image, placed.Select(p => p.Card).ToArray());
    }

    private static (double R, double G, double B) Shade(double x, double y,
        List<(PlacedCard Card, Homography ImageToCard)> placed, (double R, double G, double B) table)
    {
        foreach (var (card, toCard) in placed)
        {
            var p = toCard.Map(new Point2(x, y));
            if (!CardArt.InsideCard(p.X, p.Y)) continue;
            if (!CardArt.IsInk(card.Card, p.X, p.Y)) return (240, 240, 236);
            var ink = CardArt.InkColor(card.Card.Color);
            return (ink.R, ink.G, ink.B);
        }
        // Tisch mit leichter Maserung
        var grain = 6 * Math.Sin(y * 0.05 + Math.Sin(x * 0.01) * 3);
        return (table.R + grain, table.G + grain, table.B + grain);
    }
}
