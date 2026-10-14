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
        return result;
    }
}
