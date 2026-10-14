using SetGameSolver.Core.Model;
using SetGameSolver.Core.Synthetic;
using SetGameSolver.Core.Vision;
using Xunit.Abstractions;

namespace SetGameSolver.Core.Tests;

public class RecognitionTests(ITestOutputHelper output)
{
    private static Card[] RandomCards(int seed, int count)
    {
        var rnd = new Random(seed);
        return Card.AllCards.OrderBy(_ => rnd.Next()).Take(count).ToArray();
    }

    [Theory]
    [InlineData(1, 12, 4, false)]
    [InlineData(2, 12, 4, false)]
    [InlineData(3, 12, 3, true)]
    [InlineData(4, 15, 5, false)]
    [InlineData(5, 9, 3, false)]
    [InlineData(6, 12, 4, true)]
    [InlineData(7, 12, 4, false, 800, 600, 12)]
    [InlineData(8, 18, 6, false, 1600, 1000, 10)]
    public void Synthetic_layout_is_recognized_completely(int seed, int count, int columns, bool landscape,
        int width = 1200, int height = 900, double noise = 6)
    {
        var cards = RandomCards(seed, count);
        var scene = SceneRenderer.Render(cards, seed, width, height, columns, landscape, noise);

        var dir = Environment.GetEnvironmentVariable("SET_DEBUG_DIR");
        if (dir is not null)
            PngWriter.Write(scene.Image, Path.Combine(dir, $"scene-{seed}.png"));

        var detected = SetRecognizer.Recognize(scene.Image);
        var errors = new List<string>();
        foreach (var expected in scene.Cards)
        {
            var match = detected.MinBy(d => (d.Quad.Center - expected.Quad.Center).Length);
            if (match is null || (match.Quad.Center - expected.Quad.Center).Length > 20)
            {
                errors.Add($"nicht gefunden: {expected.Card}");
                continue;
            }
            if (match.Card != expected.Card)
                errors.Add($"erwartet {expected.Card}, erkannt {match.Card} ({match.Features})");
        }
        foreach (var e in errors) output.WriteLine(e);
        foreach (var d in detected) output.WriteLine($"{d.Card} {d.Features} unsicher={d.Uncertain}");

        Assert.Equal(cards.Length, detected.Count);
        Assert.Empty(errors);
    }

    [Fact]
    public void Every_card_is_classified_correctly_in_a_clean_patch()
    {
        var errors = new List<string>();
        foreach (var card in Card.AllCards)
        {
            var scene = SceneRenderer.Render([card], card.Index, width: 400, height: 500, columns: 1, noise: 3);
            var detected = SetRecognizer.Recognize(scene.Image);
            if (detected.Count != 1)
                errors.Add($"{card}: {detected.Count} Karten gefunden");
            else if (detected[0].Card != card)
                errors.Add($"{card}: erkannt {detected[0].Card} ({detected[0].Features})");
        }
        foreach (var e in errors) output.WriteLine(e);
        Assert.Empty(errors);
    }
}
