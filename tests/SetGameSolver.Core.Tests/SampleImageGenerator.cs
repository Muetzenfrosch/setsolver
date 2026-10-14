using SetGameSolver.Core.Model;
using SetGameSolver.Core.Solving;
using SetGameSolver.Core.Synthetic;

namespace SetGameSolver.Core.Tests;

/// <summary>
/// Erzeugt die Beispielbilder der Web-App neu (wwwroot/sample). Nur bei gesetzter Umgebungsvariable
/// SET_SAMPLE_DIR aktiv, z. B.: SET_SAMPLE_DIR=/tmp/samples dotnet test --filter SampleImageGenerator
/// </summary>
public class SampleImageGenerator
{
    [Fact]
    public void Generate()
    {
        var dir = Environment.GetEnvironmentVariable("SET_SAMPLE_DIR");
        if (dir is null)
            return;

        var rnd = new Random(7);
        Card[] Draw() => Card.AllCards.OrderBy(_ => rnd.Next()).Take(12).ToArray();

        var withSets = Draw();
        while (SetFinder.Solve(withSets).Sets.Count < 2) withSets = Draw();
        PngWriter.Write(SceneRenderer.Render(withSets, 11, 1200, 900).Image, Path.Combine(dir, "auslage-mit-sets.png"));

        var withoutSet = Draw();
        while (SetFinder.Solve(withoutSet).HasSet) withoutSet = Draw();
        PngWriter.Write(SceneRenderer.Render(withoutSet, 12, 1200, 900).Image, Path.Combine(dir, "auslage-ohne-set.png"));
    }
}
