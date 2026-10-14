using SetGameSolver.Core.Model;
using SetGameSolver.Core.Solving;

namespace SetGameSolver.Core.Tests;

public class SetFinderTests
{
    private static Card C(Number n, Color c, Shading s, Shape sh) => new(n, c, s, sh);

    [Fact]
    public void AllCards_contains_81_distinct_cards_with_matching_index()
    {
        Assert.Equal(81, Card.AllCards.Distinct().Count());
        for (var i = 0; i < 81; i++)
            Assert.Equal(i, Card.AllCards[i].Index);
    }

    [Fact]
    public void Example_from_rules_all_different_is_a_set()
    {
        var a = C(Number.One, Color.Red, Shading.Solid, Shape.Diamond);
        var b = C(Number.Two, Color.Green, Shading.Striped, Shape.Oval);
        var c = C(Number.Three, Color.Purple, Shading.Open, Shape.Squiggle);
        Assert.True(Card.IsSet(a, b, c));
    }

    [Fact]
    public void Two_same_one_different_is_not_a_set()
    {
        var a = C(Number.One, Color.Red, Shading.Solid, Shape.Diamond);
        var b = C(Number.One, Color.Red, Shading.Solid, Shape.Oval);
        var c = C(Number.Two, Color.Red, Shading.Solid, Shape.Squiggle);
        Assert.False(Card.IsSet(a, b, c));
    }

    [Fact]
    public void Any_two_distinct_cards_have_exactly_one_completing_card()
    {
        foreach (var a in Card.AllCards)
        foreach (var b in Card.AllCards.Where(x => x != a))
        {
            var completing = Card.AllCards.Where(c => Card.IsSet(a, b, c)).ToArray();
            Assert.Single(completing);
            Assert.Equal(Card.ThirdOf(a, b), completing[0]);
            Assert.NotEqual(a, completing[0]);
            Assert.NotEqual(b, completing[0]);
        }
    }

    [Fact]
    public void Full_deck_contains_1080_sets()
    {
        var result = SetFinder.Solve(Card.AllCards);
        Assert.Equal(1080, result.Sets.Count);
        Assert.True(result.HasSet);
        Assert.False(result.MustAddCards);
    }

    [Fact]
    public void Sets_are_reported_by_position_and_are_valid()
    {
        var rnd = new Random(42);
        for (var round = 0; round < 200; round++)
        {
            var cards = Card.AllCards.OrderBy(_ => rnd.Next()).Take(12).ToArray();
            var result = SetFinder.Solve(cards);

            var expected = 0;
            for (var i = 0; i < 12; i++)
            for (var j = i + 1; j < 12; j++)
            for (var k = j + 1; k < 12; k++)
                if (Card.IsSet(cards[i], cards[j], cards[k])) expected++;

            Assert.Equal(expected, result.Sets.Count);
            foreach (var s in result.Sets)
            {
                Assert.True(s.First < s.Second && s.Second < s.Third);
                Assert.True(Card.IsSet(cards[s.First], cards[s.Second], cards[s.Third]));
            }
        }
    }

    [Fact]
    public void Layout_without_any_set_requires_more_cards()
    {
        // Karten, die pro Eigenschaft nur zwei der drei Ausprägungen nutzen (2^4 = 16), sind set-frei:
        // Ein Set mit unterschiedlichen Werten bräuchte immer die dritte Ausprägung.
        var cards = Card.AllCards
            .Where(c => (byte)c.Number < 2 && (byte)c.Color < 2 && (byte)c.Shading < 2 && (byte)c.Shape < 2)
            .ToArray();
        Assert.Equal(16, cards.Length);

        var result = SetFinder.Solve(cards);
        Assert.False(result.HasSet);
        Assert.True(result.MustAddCards);
    }

    [Fact]
    public void Duplicate_cards_are_reported_and_do_not_form_sets_with_themselves()
    {
        var a = C(Number.One, Color.Red, Shading.Solid, Shape.Diamond);
        var b = C(Number.Two, Color.Red, Shading.Solid, Shape.Diamond);
        var result = SetFinder.Solve([a, a, b]);
        Assert.Equal([a], result.Duplicates);
        Assert.False(result.HasSet);
    }

    [Fact]
    public void Empty_layout_has_no_set()
    {
        var result = SetFinder.Solve([]);
        Assert.True(result.MustAddCards);
    }
}
