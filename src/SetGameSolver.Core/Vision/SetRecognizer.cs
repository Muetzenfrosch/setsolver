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
            var classified = CardClassifier.Classify(patch);
            if (classified is null)
                continue;
            var original = new Quad(quad.A * scale, quad.B * scale, quad.C * scale, quad.D * scale);
            result.Add(new DetectedCard(original, classified.Card, classified.Features, classified.Uncertain));
        }
        return SortInReadingOrder(result);
    }

    /// <summary>Sortiert Karten zeilenweise von oben links nach unten rechts, damit die Nummerierung zur Auslage passt.</summary>
    internal static List<DetectedCard> SortInReadingOrder(List<DetectedCard> cards)
    {
        if (cards.Count == 0)
            return cards;
        var heights = cards
            .Select(c => c.Quad.Corners.Max(p => p.Y) - c.Quad.Corners.Min(p => p.Y))
            .OrderBy(h => h)
            .ToArray();
        var rowTolerance = heights[heights.Length / 2] * 0.5;

        var rows = new List<List<DetectedCard>>();
        foreach (var card in cards.OrderBy(c => c.Quad.Center.Y))
        {
            var row = rows.LastOrDefault();
            if (row is not null && card.Quad.Center.Y - row.Average(c => c.Quad.Center.Y) < rowTolerance)
                row.Add(card);
            else
                rows.Add([card]);
        }
        return rows.SelectMany(r => r.OrderBy(c => c.Quad.Center.X)).ToList();
    }
}
