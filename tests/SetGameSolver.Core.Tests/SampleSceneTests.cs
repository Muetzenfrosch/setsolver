using System.Xml.Linq;
using SetGameSolver.Core.Solving;
using SetGameSolver.Core.Synthetic;

namespace SetGameSolver.Core.Tests;

public class SampleSceneTests
{
    [Fact]
    public void Every_sample_has_twelve_different_cards_and_valid_svg()
    {
        var scenes = Enumerable.Range(0, 50).Select(seed => SampleScene.Create(new Random(seed))).ToList();

        Assert.All(scenes, s =>
        {
            Assert.Equal(12, s.Cards.Distinct().Count());
            var svg = XDocument.Parse(s.Svg).Root!;
            Assert.Equal(s.Width, (int)svg.Attribute("width")!);
            Assert.Equal(s.Height, (int)svg.Attribute("height")!);
        });
        // Jedes Mal andere Karten, mal mit, mal ohne Set, hochkant und quer.
        Assert.Equal(scenes.Count, scenes.Select(s => string.Join(",", s.Cards.OrderBy(c => c.Index))).Distinct().Count());
        Assert.Contains(scenes, s => !SetFinder.Solve(s.Cards.ToArray()).HasSet);
        Assert.True(scenes.Count(s => SetFinder.Solve(s.Cards.ToArray()).HasSet) > scenes.Count / 2);
        Assert.Contains(scenes, s => s.Width > s.Height);
        Assert.Contains(scenes, s => s.Width < s.Height);
    }
}
