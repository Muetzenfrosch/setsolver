using SetGameSolver.Core.Model;
using SetGameSolver.Core.Vision;
using StbImageSharp;
using Xunit.Abstractions;

namespace SetGameSolver.Core.Tests;

/// <summary>
/// Erkennung an echten Fotos (Photos/labels.txt beschreibt die tatsächliche Auslage, Photos/layouts.txt ihr
/// Raster). Neue Fotos dort ablegen und beschriften; die Ausgabe des Tests zeigt je Foto, was falsch oder nicht
/// erkannt wurde.
/// Mit SET_DEBUG_DIR werden Überlagerungsbilder und entzerrte Karten zum Ansehen gespeichert.
/// </summary>
public class PhotoTests(ITestOutputHelper output)
{
    private static readonly string PhotoDir = Path.Combine(AppContext.BaseDirectory, "Photos");

    // Stand 15.10.2026 (27 Kunstlicht- und 15 Tageslicht-Fotos): 501 von 513 Karten richtig, 1 falsch,
    // 11 nicht gefunden. Die Grenzen schützen vor Verschlechterungen; bei Verbesserungen bitte nachziehen.
    private const int MinCorrect = 498;
    private const int MaxWrong = 2;

    [Fact]
    public void Real_photos_are_recognized()
    {
        int correct = 0, wrong = 0, total = 0;
        var layouts = Layouts();
        var wrongLayouts = new List<string>();
        foreach (var (name, expected) in Labels().OrderBy(l => l.Key))
        {
            var image = Load(Path.Combine(PhotoDir, name + ".jpg"));
            var detected = SetRecognizer.Recognize(image);
            // Doppelt erkannte Karten zählen als Fehler.
            var ok = detected.Select(d => d.Card).Distinct().Count(expected.Contains);
            var bad = detected.Where(d => !expected.Contains(d.Card)).ToList();
            var duplicates = detected.Count - ok - bad.Count;
            correct += ok;
            wrong += bad.Count + duplicates;
            total += expected.Count;
            output.WriteLine($"{name}: {ok}/{expected.Count} richtig, {bad.Count} falsch, {duplicates} doppelt");
            foreach (var d in bad)
                output.WriteLine($"   falsch: {d.Card} {d.Features}");

            var grid = CardLayout.Arrange(detected.Select(d => d.Quad).ToList());
            var layout = $"{grid.Rows}x{grid.Columns}";
            if (layout != layouts[name])
            {
                wrongLayouts.Add($"{name} {layout} statt {layouts[name]}");
                output.WriteLine($"   Raster {layout} statt {layouts[name]}");
            }

            var dir = Environment.GetEnvironmentVariable("SET_DEBUG_DIR");
            if (dir is not null)
                DebugImages.Write(image, detected, expected, Path.Combine(dir, name));
        }
        output.WriteLine($"Gesamt: {correct}/{total} richtig, {wrong} falsch");

        Assert.True(correct >= MinCorrect, $"Nur {correct}/{total} Karten richtig erkannt (Mindestwert {MinCorrect}).");
        Assert.True(wrong <= MaxWrong, $"{wrong} Karten falsch erkannt (höchstens {MaxWrong}).");
        Assert.Empty(wrongLayouts);
    }

    internal static RgbaImage Load(string path)
    {
        using var stream = File.OpenRead(path);
        var result = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        return new RgbaImage(result.Width, result.Height, result.Data);
    }

    internal static Dictionary<string, HashSet<Card>> Labels()
    {
        var labels = new Dictionary<string, HashSet<Card>>();
        foreach (var line in File.ReadAllLines(Path.Combine(PhotoDir, "labels.txt")))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var parts = line.Split(':');
            var cards = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Parse).ToHashSet();
            foreach (var name in parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                labels[name] = cards;
        }
        return labels;
    }

    /// <summary>Erwartetes Raster je Foto, z. B. "3x4" (Zeilen x Spalten).</summary>
    internal static Dictionary<string, string> Layouts() => File.ReadAllLines(Path.Combine(PhotoDir, "layouts.txt"))
        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
        .Select(line => line.Split(':', StringSplitOptions.TrimEntries))
        .ToDictionary(parts => parts[0], parts => parts[1]);

    private static Card Parse(string code) => new(
        (Number)(code[0] - '1'),
        code[1] switch { 'R' => Color.Red, 'G' => Color.Green, _ => Color.Purple },
        code[2] switch { 's' => Shading.Solid, 't' => Shading.Striped, _ => Shading.Open },
        code[3] switch { 'D' => Shape.Diamond, 'O' => Shape.Oval, _ => Shape.Squiggle });
}
