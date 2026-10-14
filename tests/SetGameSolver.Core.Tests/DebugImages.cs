using SetGameSolver.Core.Model;
using SetGameSolver.Core.Vision;

namespace SetGameSolver.Core.Tests;

/// <summary>Schreibt Hilfsbilder zur Fehlersuche: Foto mit erkannten Kartenumrissen und alle entzerrten Karten.</summary>
internal static class DebugImages
{
    public static void Write(RgbaImage image, List<DetectedCard> detected, HashSet<Card> expected, string prefix)
    {
        var overlay = new RgbaImage(image.Width, image.Height, (byte[])image.Pixels.Clone());
        foreach (var d in detected)
        {
            var ok = expected.Contains(d.Card);
            var c = d.Quad.Corners;
            for (var k = 0; k < 4; k++)
                Line(overlay, c[k], c[(k + 1) % 4], ok ? (byte)0 : (byte)255, ok ? (byte)200 : (byte)0, 0);
        }
        PngWriter.Write(overlay, prefix + "-overlay.png");

        if (detected.Count == 0) return;
        var work = image.DownscaleTo(SetRecognizer.WorkingMaxSide);
        var scale = (double)work.Width / image.Width;
        var patches = detected.Select(d => CardPatch.Extract(work,
            new Quad(d.Quad.A * scale, d.Quad.B * scale, d.Quad.C * scale, d.Quad.D * scale))).ToList();
        var w = patches[0].Width;
        var h = patches[0].Height;
        var sheet = new RgbaImage(w * patches.Count, h);
        for (var p = 0; p < patches.Count; p++)
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            sheet.Set(p * w + x, y, (byte)patches[p].R[i], (byte)patches[p].G[i], (byte)patches[p].B[i]);
        }
        PngWriter.Write(sheet, prefix + "-cards.png");
    }

    private static void Line(RgbaImage img, Point2 a, Point2 b, byte r, byte g, byte bl)
    {
        var steps = (int)Math.Max(1, (b - a).Length);
        for (var s = 0; s <= steps; s++)
        {
            var p = a + (b - a) * (s / (double)steps);
            for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
            {
                var x = (int)p.X + dx;
                var y = (int)p.Y + dy;
                if (x >= 0 && y >= 0 && x < img.Width && y < img.Height) img.Set(x, y, r, g, bl);
            }
        }
    }
}
