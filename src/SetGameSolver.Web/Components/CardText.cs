using SetGameSolver.Core.Model;

namespace SetGameSolver.Web.Components;

/// <summary>Deutsche Bezeichnungen und Anzeigefarben der Karteneigenschaften.</summary>
public static class CardText
{
    public static string Name(Number n) => n switch { Number.One => "1", Number.Two => "2", _ => "3" };
    public static string Name(Color c) => c switch { Color.Red => "Rot", Color.Green => "Grün", _ => "Lila" };
    public static string Name(Shading s) => s switch { Shading.Solid => "Voll", Shading.Striped => "Gestreift", _ => "Leer" };
    public static string Name(Shape s) => s switch { Shape.Diamond => "Raute", Shape.Oval => "Oval", _ => "Welle" };

    public static string CssColor(Color c) => SetGameSolver.Core.Synthetic.CardShapes.InkColor(c);

    public static string Describe(Card c) =>
        $"{Name(c.Number)} × {Name(c.Color)}, {Name(c.Shading)}, {Name(c.Shape)}";
}
