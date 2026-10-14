using SetGameSolver.Core.Model;

namespace SetGameSolver.Core.Solving;

/// <summary>Ein gefundenes Set, angegeben über die Positionen der Karten in der Auslage.</summary>
public readonly record struct FoundSet(int First, int Second, int Third)
{
    public IEnumerable<int> Positions => [First, Second, Third];
}

/// <summary>Ergebnis der Auswertung einer Auslage.</summary>
public sealed record SolveResult(IReadOnlyList<Card> Cards, IReadOnlyList<FoundSet> Sets)
{
    public bool HasSet => Sets.Count > 0;

    /// <summary>
    /// Kein Set in der Auslage: Nach den Spielregeln werden drei Karten nachgelegt.
    /// </summary>
    public bool MustAddCards => Sets.Count == 0;

    /// <summary>Doppelt erkannte Karten deuten auf einen Erkennungsfehler hin (jede Karte gibt es nur einmal).</summary>
    public IReadOnlyList<Card> Duplicates { get; } =
        Cards.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
}

public static class SetFinder
{
    /// <summary>Findet alle Sets in einer Auslage beliebiger Größe.</summary>
    public static SolveResult Solve(IReadOnlyList<Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        // Position jeder Karte merken, damit für jedes Paar die dritte Karte in O(1) gefunden wird.
        var positions = new Dictionary<Card, List<int>>();
        for (var i = 0; i < cards.Count; i++)
        {
            if (!positions.TryGetValue(cards[i], out var list))
                positions[cards[i]] = list = [];
            list.Add(i);
        }

        var sets = new List<FoundSet>();
        for (var i = 0; i < cards.Count; i++)
        {
            for (var j = i + 1; j < cards.Count; j++)
            {
                if (cards[i] == cards[j])
                    continue;
                if (!positions.TryGetValue(Card.ThirdOf(cards[i], cards[j]), out var thirds))
                    continue;
                foreach (var k in thirds)
                {
                    if (k > j)
                        sets.Add(new FoundSet(i, j, k));
                }
            }
        }

        return new SolveResult(cards, sets);
    }
}
