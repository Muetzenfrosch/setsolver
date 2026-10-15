using SetGameSolver.Core.Model;
using SetGameSolver.Core.Solving;
using static SetGameSolver.Core.Synthetic.CardShapes;

namespace SetGameSolver.Core.Synthetic;

/// <summary>
/// Beispielbild für die App: 12 zufällige, verschiedene Karten mit den Symbolen der echten Karten, leicht
/// verdreht auf einem Tisch. Als SVG, das der Browser selbst zeichnet (schneller als Pixel in .NET).
/// </summary>
/// <param name="Cards">Die Karten zeilenweise von oben links.</param>
public sealed record SampleScene(string Svg, int Width, int Height, IReadOnlyList<Card> Cards)
{
    public const int CardCount = 12;

    private static readonly (string Color, bool Grain)[] Tables =
    [
        ("#5b3b2a", true),  // Holz
        ("#3f2b21", true),  // dunkles Holz
        ("#2e5b45", false), // grüner Filz
        ("#38424c", false), // Schiefer
        ("#2f4a6b", false), // blaue Decke
    ];

    /// <summary>
    /// Zieht 12 verschiedene Karten; etwa jedes fünfte Beispiel enthält kein Set, damit auch „nachlegen“ vorkommt.
    /// Die Karten liegen hochkant in 3 Reihen zu 4 oder quer in 4 Reihen zu 3.
    /// </summary>
    public static SampleScene Create(Random random)
    {
        var withoutSet = random.Next(5) == 0;
        Card[] cards;
        do cards = Card.AllCards.OrderBy(_ => random.Next()).Take(CardCount).ToArray();
        while (SetFinder.Solve(cards).HasSet == withoutSet);

        var landscape = random.Next(2) == 0;
        var (rows, columns) = landscape ? (4, 3) : (3, 4);
        var (width, height) = landscape ? (1200, 900) : (900, 1200); // wie ein Handyfoto der Auslage
        var (cardW, cardH) = landscape ? (CardShapes.Height, CardShapes.Width) : (CardShapes.Width, CardShapes.Height);
        const double gap = 1.2; // Mittenabstand in Kartengrößen
        var scale = 0.88 * Math.Min(width / (columns * gap * cardW), height / (rows * gap * cardH));
        var (cellW, cellH) = (gap * cardW * scale, gap * cardH * scale);
        var (originX, originY) = ((width - columns * cellW) / 2, (height - rows * cellH) / 2);

        var (table, grain) = Tables[random.Next(Tables.Length)];
        var svg = new System.Text.StringBuilder();
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        svg.Append("<defs>").Append(StripePatterns("s-"));
        svg.Append("<filter id=\"shadow\" x=\"-20%\" y=\"-20%\" width=\"140%\" height=\"140%\">" +
                   "<feDropShadow dx=\"0.4\" dy=\"0.8\" stdDeviation=\"1\" flood-color=\"#000\" flood-opacity=\"0.45\"/></filter>");
        // Licht fällt schräg ein: eine Ecke etwas heller, die gegenüberliegende etwas dunkler.
        var (lx, ly) = (random.Next(2), random.Next(2));
        svg.Append($"<linearGradient id=\"light\" x1=\"{lx}\" y1=\"{ly}\" x2=\"{1 - lx}\" y2=\"{1 - ly}\">" +
                   "<stop offset=\"0\" stop-color=\"#fff\" stop-opacity=\"0.10\"/>" +
                   "<stop offset=\"1\" stop-color=\"#000\" stop-opacity=\"0.18\"/></linearGradient>");
        svg.Append("</defs>");
        svg.Append($"<rect width=\"{width}\" height=\"{height}\" fill=\"{table}\"/>");
        if (grain)
        {
            for (var k = 0; k < 40; k++)
            {
                var y = random.NextDouble() * height;
                var bend = (random.NextDouble() - 0.5) * 30;
                svg.Append($"<path d=\"M 0 {F(y)} Q {F(width / 2.0)} {F(y + bend)} {width} {F(y + (random.NextDouble() - 0.5) * 10)}\" " +
                           $"stroke=\"#000\" stroke-opacity=\"{F(0.05 + 0.1 * random.NextDouble())}\" " +
                           $"stroke-width=\"{F(1 + 4 * random.NextDouble())}\" fill=\"none\"/>");
            }
        }

        for (var k = 0; k < cards.Length; k++)
        {
            var cx = originX + (k % columns + 0.5) * cellW + (random.NextDouble() - 0.5) * 0.08 * cellW;
            var cy = originY + (k / columns + 0.5) * cellH + (random.NextDouble() - 0.5) * 0.08 * cellH;
            var angle = (random.NextDouble() - 0.5) * 8 + (landscape ? 90 : 0) + (random.Next(2) == 0 ? 0 : 180);
            svg.Append($"<g transform=\"translate({F(cx)} {F(cy)}) rotate({F(angle)}) scale({F(scale)}) " +
                       $"translate({F(-CardShapes.Width / 2)} {F(-CardShapes.Height / 2)})\">");
            svg.Append($"<rect width=\"{F(CardShapes.Width)}\" height=\"{F(CardShapes.Height)}\" rx=\"{F(CornerRadius)}\" fill=\"#f7f6f1\" filter=\"url(#shadow)\"/>");
            svg.Append(SymbolsSvg(cards[k], "s-"));
            svg.Append("</g>");
        }
        svg.Append($"<rect width=\"{width}\" height=\"{height}\" fill=\"url(#light)\"/>");
        svg.Append("</svg>");
        return new SampleScene(svg.ToString(), width, height, cards);
    }
}
