using SetGameSolver.Core.Model;

namespace SetGameSolver.Web.Components;

/// <summary>Anzeigefarben der Karteneigenschaften (die Bezeichnungen stehen in <see cref="Localization.Texts"/>).</summary>
public static class CardText
{
    public static string CssColor(Color c) => SetGameSolver.Core.Synthetic.CardShapes.InkColor(c);
}
