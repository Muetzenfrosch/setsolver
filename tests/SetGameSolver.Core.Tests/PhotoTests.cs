using SetGameSolver.Core.Model;
using SetGameSolver.Core.Vision;
using StbImageSharp;
using Xunit.Abstractions;

namespace SetGameSolver.Core.Tests;

/// <summary>
/// Erkennung an echten Fotos (Photos/labels.txt beschreibt die tatsächliche Auslage). Neue Fotos dort
/// ablegen und beschriften; die Ausgabe des Tests zeigt je Foto, was falsch oder nicht erkannt wurde.
/// Mit SET_DEBUG_DIR werden Überlagerungsbilder und entzerrte Karten zum Ansehen gespeichert.
/// </summary>
public class PhotoTests(ITestOutputHelper output)
{
    private static readonly string PhotoDir = Path.Combine(AppContext.BaseDirectory, "Photos");

    // Stand 14.10.2026 (Kunstlicht-Fotos): 182 von 189 Karten richtig, 1 falsch, 6 nicht gefunden.
    // Die Grenzen schützen vor Verschlechterungen; bei Verbesserungen bitte nachziehen.
    private const int MinCorrect = 180;
    private const int MaxWrong = 2;

    [Fact]
    public void Real_photos_are_recognized()
    {
        int correct = 0, wrong = 0, total = 0;
        foreach (var (name, expected) in Labels().OrderBy(l => l.Key))
        {
            var image = Load(Path.Combine(PhotoDir, name + ".jpg"));
            var detected = SetRecognizer.Recognize(image);
            var ok = detected.Count(d => expected.Contains(d.Card));
            var bad = detected.Where(d => !expected.Contains(d.Card)).ToList();
            correct += ok;
            wrong += bad.Count;
            total += expected.Count;
            output.WriteLine($"{name}: {ok}/{expected.Count} richtig, {bad.Count} falsch");
            foreach (var d in bad)
                output.WriteLine($"   falsch: {d.Card} {d.Features}");

            var dir = Environment.GetEnvironmentVariable("SET_DEBUG_DIR");
            if (dir is not null)
                DebugImages.Write(image, detected, expected, Path.Combine(dir, name));
        }
        output.WriteLine($"Gesamt: {correct}/{total} richtig, {wrong} falsch");

        Assert.True(correct >= MinCorrect, $"Nur {correct}/{total} Karten richtig erkannt (Mindestwert {MinCorrect}).");
        Assert.True(wrong <= MaxWrong, $"{wrong} Karten falsch erkannt (höchstens {MaxWrong}).");
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

    private static Card Parse(string code) => new(
        (Number)(code[0] - '1'),
        code[1] switch { 'R' => Color.Red, 'G' => Color.Green, _ => Color.Purple },
        code[2] switch { 's' => Shading.Solid, 't' => Shading.Striped, _ => Shading.Open },
        code[3] switch { 'D' => Shape.Diamond, 'O' => Shape.Oval, _ => Shape.Squiggle });
}
