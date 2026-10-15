using System.Globalization;
using SetGameSolver.Core.Model;

namespace SetGameSolver.Core.Synthetic;

/// <summary>
/// Symbole wie auf den echten Karten, als SVG (Einheit ≈ mm, Karte 57 × 89 im Hochformat): Symbole ≈ 37 × 18,
/// Abstand der Mitten ≈ 25,7, feine senkrechte Streifen. Die Welle ist punktsymmetrisch und wurde aus Fotos
/// echter Karten abgenommen. Genutzt von der Kartenansicht der App und den Beispielbildern.
/// </summary>
public static class CardShapes
{
    public const double Width = 57;
    public const double Height = 89;
    public const double CornerRadius = 4;
    public const double SymbolSpacing = 25.7;
    public const double StrokeWidth = 1.0;
    public const string DiamondPoints = "-19,0 0,-9.1 19,0 0,9.1";
    public const double OvalWidth = 37.5;
    public const double OvalHeight = 17.5;
    public const string SquigglePath =
        "M -18.1 2.5 C -17.9 1.2 -17.4 -0.7 -16.8 -2.1 C -16.1 -3.4 -15.3 -4.9 -14.2 -5.8 " +
        "C -13.1 -6.6 -11.6 -7 -10.3 -7.3 C -9 -7.6 -7.6 -7.6 -6.2 -7.5 C -4.8 -7.4 -3.4 -7 -2.1 -6.6 " +
        "C -0.7 -6.2 0.7 -5.4 2.1 -5.2 C 3.4 -4.9 4.8 -4.7 6.2 -4.8 C 7.6 -4.9 9 -5.3 10.3 -5.8 " +
        "C 11.6 -6.3 12.9 -7.5 13.9 -7.9 C 14.9 -8.3 15.6 -8.5 16.3 -8.1 C 16.9 -7.7 17.6 -6.5 17.9 -5.6 " +
        "C 18.2 -4.6 18.3 -3.7 18.1 -2.5 C 17.9 -1.2 17.4 0.7 16.8 2.1 C 16.1 3.4 15.3 4.9 14.2 5.8 " +
        "C 13.1 6.6 11.6 7 10.3 7.3 C 9 7.6 7.6 7.6 6.2 7.5 C 4.8 7.4 3.4 7 2.1 6.6 " +
        "C 0.7 6.2 -0.7 5.4 -2.1 5.2 C -3.4 4.9 -4.8 4.7 -6.2 4.8 C -7.6 4.9 -9 5.3 -10.3 5.8 " +
        "C -11.6 6.3 -12.9 7.5 -13.9 7.9 C -14.9 8.3 -15.6 8.5 -16.3 8.1 C -16.9 7.7 -17.6 6.5 -17.9 5.6 " +
        "C -18.2 4.6 -18.3 3.7 -18.1 2.5 Z";

    // Streifenmuster: senkrechte Linien der Breite 0,4 im Abstand 1,1.
    public const double StripePeriod = 1.1;
    public const double StripeWidth = 0.4;

    /// <summary>Druckfarben, gemessen an Fotos echter Karten.</summary>
    public static string InkColor(Color color) => color switch
    {
        Color.Red => "#e6462d",
        Color.Green => "#11924a",
        _ => "#54289b",
    };

    /// <summary>Senkrechte Mitte des i-ten von count Symbolen.</summary>
    public static double SymbolCenterY(int i, int count) => Height / 2 + (i - (count - 1) / 2.0) * SymbolSpacing;

    /// <summary>
    /// SVG-Elemente der Symbole einer Karte (Kartenkoordinaten). Streifen verweisen auf
    /// <c>url(#{stripePrefix}red)</c> usw., siehe <see cref="StripePatterns"/>.
    /// </summary>
    public static string SymbolsSvg(Card card, string stripePrefix)
    {
        var ink = InkColor(card.Color);
        var fill = card.Shading switch
        {
            Shading.Solid => ink,
            Shading.Striped => $"url(#{stripePrefix}{card.Color.ToString().ToLowerInvariant()})",
            _ => "none",
        };
        var paint = $"fill=\"{fill}\" stroke=\"{ink}\" stroke-width=\"{F(StrokeWidth)}\"";
        var shape = card.Shape switch
        {
            Shape.Diamond => $"<polygon points=\"{DiamondPoints}\" {paint}/>",
            Shape.Oval => $"<rect x=\"{F(-OvalWidth / 2)}\" y=\"{F(-OvalHeight / 2)}\" width=\"{F(OvalWidth)}\" " +
                          $"height=\"{F(OvalHeight)}\" rx=\"{F(OvalHeight / 2)}\" {paint}/>",
            _ => $"<path d=\"{SquigglePath}\" {paint}/>",
        };
        var count = (int)card.Number + 1;
        return string.Concat(Enumerable.Range(0, count).Select(i =>
            $"<g transform=\"translate({F(Width / 2)} {F(SymbolCenterY(i, count))})\">{shape}</g>"));
    }

    /// <summary>Die drei Streifenmuster als SVG-&lt;pattern&gt;-Elemente für &lt;defs&gt;.</summary>
    public static string StripePatterns(string stripePrefix) => string.Concat(
        new[] { Color.Red, Color.Green, Color.Purple }.Select(c =>
            $"<pattern id=\"{stripePrefix}{c.ToString().ToLowerInvariant()}\" width=\"{F(StripePeriod)}\" height=\"4\" " +
            $"patternUnits=\"userSpaceOnUse\"><rect width=\"{F(StripeWidth)}\" height=\"4\" fill=\"{InkColor(c)}\"/></pattern>"));

    internal static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
