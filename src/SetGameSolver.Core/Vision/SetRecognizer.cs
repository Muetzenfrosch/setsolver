using SetGameSolver.Core.Model;

namespace SetGameSolver.Core.Vision;

/// <summary>Eine im Foto erkannte Karte mit Lage und Merkmalen.</summary>
public sealed record DetectedCard(Quad Quad, Card Card, CardFeatures Features, bool Uncertain);

/// <summary>Verbindet Kartensuche und Klassifizierung: Foto rein, erkannte Karten raus.</summary>
public static class SetRecognizer
{
    /// <summary>Größere Fotos werden vorab verkleinert; das reicht für die Erkennung und spart Rechenzeit im Browser.</summary>
    public const int WorkingMaxSide = 1600;

    /// <summary>Werden weniger helle Karten gefunden, wird zusätzlich nach dunklen Karten gesucht.</summary>
    private const int MinLightCards = 3;

    public static List<DetectedCard> Recognize(RgbaImage image)
    {
        var work = image.DownscaleTo(WorkingMaxSide);
        var scale = (double)image.Width / work.Width;

        var result = Detect(work, darkCards: false);
        // Dunkelmodus mancher Set-Apps: schwarze Karten mit grauem Rand auf schwarzem Grund. Im Negativ sind es
        // helle Karten, die wie üblich gefunden werden; nur versuchen, wenn kaum helle Karten zu sehen sind.
        if (result.Count < MinLightCards)
        {
            var dark = Detect(work, darkCards: true);
            if (dark.Count > result.Count)
                result = dark;
        }
        result = result
            .Select(d => d with { Quad = new Quad(d.Quad.A * scale, d.Quad.B * scale, d.Quad.C * scale, d.Quad.D * scale) })
            .ToList();

        // Zeilenweise von oben links nach unten rechts, damit die Nummerierung zum Raster der Auslage passt.
        var grid = CardLayout.Arrange(result.Select(d => d.Quad).ToList());
        return result
            .Select((card, i) => (Card: card, Cell: grid.Cells[i]))
            .OrderBy(t => t.Cell.Row)
            .ThenBy(t => t.Cell.Column)
            .Select(t => t.Card)
            .ToList();
    }

    private static List<DetectedCard> Detect(RgbaImage work, bool darkCards)
    {
        var result = new List<DetectedCard>();
        foreach (var quad in CardDetector.FindCards(darkCards ? work.Inverted() : work))
        {
            var patch = CardPatch.Extract(work, quad);
            if (darkCards)
                patch = patch.DarkPaperToWhite();
            var classified = CardClassifier.Classify(patch, CutOff(quad, work.Width, work.Height));
            if (classified is not null)
                result.Add(new DetectedCard(quad, classified.Card, classified.Features, classified.Uncertain));
        }
        return result;
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
