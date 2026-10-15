using SetGameSolver.Core.Model;

namespace SetGameSolver.Core.Vision;

/// <summary>Eine im Foto erkannte Karte mit Lage und Merkmalen.</summary>
public sealed record DetectedCard(Quad Quad, Card Card, CardFeatures Features, bool Uncertain);

/// <summary>Verbindet Kartensuche und Klassifizierung: Foto rein, erkannte Karten raus.</summary>
public static class SetRecognizer
{
    /// <summary>Größere Fotos werden vorab verkleinert; das reicht für die Erkennung und spart Rechenzeit im Browser.</summary>
    public const int WorkingMaxSide = 1600;

    public static List<DetectedCard> Recognize(RgbaImage image)
    {
        var work = image.DownscaleTo(WorkingMaxSide);
        var scale = (double)image.Width / work.Width;

        var result = new List<DetectedCard>();
        foreach (var quad in CardDetector.FindCards(work))
        {
            var patch = CardPatch.Extract(work, quad);
            var classified = CardClassifier.Classify(patch, CutOff(quad, work.Width, work.Height));
            if (classified is null)
                continue;
            var original = new Quad(quad.A * scale, quad.B * scale, quad.C * scale, quad.D * scale);
            result.Add(new DetectedCard(original, classified.Card, classified.Features, classified.Uncertain));
        }
        // Zeilenweise von oben links nach unten rechts, damit die Nummerierung zum Raster der Auslage passt.
        var grid = CardLayout.Arrange(result.Select(d => d.Quad).ToList());
        return result
            .Select((card, i) => (Card: card, Cell: grid.Cells[i]))
            .OrderBy(t => t.Cell.Row)
            .ThenBy(t => t.Cell.Column)
            .Select(t => t.Card)
            .ToList();
    }

    /// <summary>Liegt eine Kartenseite auf dem Bildrand, ist die Karte vermutlich abgeschnitten.</summary>
    internal static bool CutOff(Quad quad, int width, int height)
    {
        var margin = 0.006 * Math.Max(width, height);
        int Count(Func<Point2, bool> near) => quad.Corners.Count(near);
        return Count(p => p.X < margin) >= 2 || Count(p => p.Y < margin) >= 2
            || Count(p => p.X > width - 1 - margin) >= 2 || Count(p => p.Y > height - 1 - margin) >= 2;
    }
}
