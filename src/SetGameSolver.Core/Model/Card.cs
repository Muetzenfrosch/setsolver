namespace SetGameSolver.Core.Model;

/// <summary>
/// Eine Set-Karte mit ihren vier Eigenschaften. Jede Eigenschaft hat drei Ausprägungen,
/// es gibt also 3^4 = 81 verschiedene Karten.
/// </summary>
public readonly record struct Card(Number Number, Color Color, Shading Shading, Shape Shape)
{
    /// <summary>Alle 81 Karten des Spiels.</summary>
    public static IReadOnlyList<Card> AllCards { get; } =
        Enumerable.Range(0, 81).Select(FromIndex).ToArray();

    /// <summary>Eindeutiger Index 0..80 (Basis-3-Kodierung der Eigenschaften).</summary>
    public int Index => (byte)Number * 27 + (byte)Color * 9 + (byte)Shading * 3 + (byte)Shape;

    public static Card FromIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 80);
        return new Card((Number)(index / 27), (Color)(index / 9 % 3), (Shading)(index / 3 % 3), (Shape)(index % 3));
    }

    /// <summary>
    /// Die eindeutige Karte, die zusammen mit <paramref name="a"/> und <paramref name="b"/> ein Set bildet.
    /// Pro Eigenschaft gilt: gleich bleibt gleich, verschieden ergänzt sich zur dritten Ausprägung.
    /// </summary>
    public static Card ThirdOf(Card a, Card b) => new(
        (Number)Complete((byte)a.Number, (byte)b.Number),
        (Color)Complete((byte)a.Color, (byte)b.Color),
        (Shading)Complete((byte)a.Shading, (byte)b.Shading),
        (Shape)Complete((byte)a.Shape, (byte)b.Shape));

    /// <summary>Prüft, ob drei Karten ein Set bilden.</summary>
    public static bool IsSet(Card a, Card b, Card c) => a != b && ThirdOf(a, b) == c;

    private static int Complete(int x, int y) => (6 - x - y) % 3;

    public override string ToString() => $"{Number}-{Color}-{Shading}-{Shape}";
}
