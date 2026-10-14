using SetGameSolver.Core.Model;

namespace SetGameSolver.Core.Synthetic;

/// <summary>
/// Beschreibt eine Set-Karte geometrisch (Hochformat, Einheiten in mm, Ursprung oben links).
/// Dient für synthetische Testbilder und das Beispielbild in der App.
/// </summary>
public static class CardArt
{
    public const double Width = 57;
    public const double Height = 89;
    private const double CornerRadius = 3.5;
    private const double HalfLength = 19;   // halbe Symbolbreite
    private const double HalfHeight = 8.5;  // halbe Symbolhöhe
    private const double Spacing = 21;      // Abstand der Symbolmitten
    private const double Stroke = 1.1;
    private const double StripePeriod = 1.3;

    public static (byte R, byte G, byte B) InkColor(Color color) => color switch
    {
        Color.Red => (222, 28, 44),
        Color.Green => (12, 150, 70),
        _ => (102, 40, 145),
    };

    public static bool InsideCard(double x, double y)
    {
        if (x < 0 || y < 0 || x > Width || y > Height) return false;
        var dx = Math.Max(0, Math.Max(CornerRadius - x, x - (Width - CornerRadius)));
        var dy = Math.Max(0, Math.Max(CornerRadius - y, y - (Height - CornerRadius)));
        return dx * dx + dy * dy <= CornerRadius * CornerRadius;
    }

    /// <summary>Ist an dieser Stelle der Karte Tinte? (Innerhalb der Karte, außerhalb: false.)</summary>
    public static bool IsInk(Card card, double x, double y)
    {
        var count = (int)card.Number + 1;
        for (var i = 0; i < count; i++)
        {
            var cy = Height / 2 + (i - (count - 1) / 2.0) * Spacing;
            var lx = x - Width / 2;
            var ly = y - cy;
            if (Math.Abs(ly) > HalfHeight + 1 || Math.Abs(lx) > HalfLength + 1) continue;
            if (!InShape(card.Shape, lx, ly)) continue;

            var outline = !InShape(card.Shape, lx + Stroke, ly) || !InShape(card.Shape, lx - Stroke, ly)
                || !InShape(card.Shape, lx, ly + Stroke) || !InShape(card.Shape, lx, ly - Stroke);
            return card.Shading switch
            {
                Shading.Solid => true,
                Shading.Open => outline,
                _ => outline || PositiveMod(lx, StripePeriod) < StripePeriod * 0.35,
            };
        }
        return false;
    }

    private static double PositiveMod(double v, double m) => ((v % m) + m) % m;

    private static bool InShape(Shape shape, double x, double y)
    {
        switch (shape)
        {
            case Shape.Diamond:
                return Math.Abs(x) / HalfLength + Math.Abs(y) / HalfHeight <= 1;
            case Shape.Oval:
                var dx = Math.Max(0, Math.Abs(x) - (HalfLength - HalfHeight));
                return dx * dx + y * y <= HalfHeight * HalfHeight;
            default:
                // Geschwungenes Band: Mittellinie als Sinuswelle, Dicke zu den Enden hin abnehmend.
                if (Math.Abs(x) > HalfLength) return false;
                var t = x / HalfLength;
                var center = 0.38 * HalfHeight * Math.Sin(Math.PI * t);
                var half = 0.62 * HalfHeight * Math.Sqrt(Math.Max(0, 1 - Math.Pow(t, 4)));
                return Math.Abs(y - center) <= half;
        }
    }
}
