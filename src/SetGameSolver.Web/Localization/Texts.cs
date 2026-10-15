using System.Globalization;
using SetGameSolver.Core.Model;

namespace SetGameSolver.Web.Localization;

/// <summary>Alle Texte der Oberfläche, je einmal auf Deutsch und auf Englisch.</summary>
public sealed class Texts
{
    public static readonly Texts German = new(german: true);
    public static readonly Texts English = new(german: false);

    private readonly bool _german;

    private Texts(bool german) => _german = german;

    /// <summary>Sprachcode für das lang-Attribut und die gespeicherte Auswahl.</summary>
    public string Code => _german ? "de" : "en";

    private string S(string de, string en) => _german ? de : en;

    // Kopf und Aktionen
    public string Language => S("Sprache", "Language");
    public string TakePhoto => S("Foto aufnehmen", "Take photo");
    public string ChooseImage => S("Bild wählen", "Choose image");
    public string Example => S("Beispiel", "Example");
    public string Recognizing => S("Erkenne Karten …", "Recognizing cards …");
    public string ImageFailed(string reason) =>
        S($"Das Bild konnte nicht ausgewertet werden: {reason}", $"The image could not be analyzed: {reason}");
    public string PhotoAlt => S("Foto der Kartenauslage", "Photo of the cards on the table");

    // Ergebnis
    public string NoCards => S("Keine Karten erkannt.", "No cards recognized.");
    public string NoCardsTip => S("Tipp: Karten auf dunklen Untergrund legen und von oben fotografieren.",
                                  "Tip: Place the cards on a dark surface and take the photo from above.");
    public string SetsFound(int count) => count == 1
        ? S("1 Set gefunden", "1 set found")
        : S($"{count} Sets gefunden", $"{count} sets found");
    public string CardsRecognized(int count, double? seconds)
    {
        var time = seconds is double s ? $" in {Seconds(s)}" : "";
        return S($"{count} Karten erkannt{time}.", count == 1 ? $"1 card recognized{time}." : $"{count} cards recognized{time}.");
    }
    public string NoSet => S("Kein Set: 3 Karten nachlegen.", "No set: add 3 more cards.");
    public string NoSetDetail(int count) =>
        S($"{count} Karten erkannt, keine drei davon bilden ein Set.",
          count == 1 ? "1 card recognized, no three of them form a set." : $"{count} cards recognized, no three of them form a set.");
    public string Duplicates(string cards) =>
        S($"Achtung: Karte doppelt erkannt ({cards}). Bitte prüfen.", $"Attention: card recognized twice ({cards}). Please check.");
    public string Uncertain(int count) => count == 1
        ? S("1 Karte ist unsicher erkannt (gestrichelt). Antippen zum Korrigieren.",
            "1 card is uncertain (dashed). Tap to correct it.")
        : S($"{count} Karten sind unsicher erkannt (gestrichelt). Antippen zum Korrigieren.",
            $"{count} cards are uncertain (dashed). Tap to correct them.");

    // Erkannte Karten
    public string RecognizedCards => S("Erkannte Karten", "Recognized cards");
    public string TapToCorrect => S("Antippen, um eine falsch erkannte Karte zu korrigieren.",
                                    "Tap a card to correct it if it was recognized wrongly.");
    public string FreeCellHint => S(" Ein freier Platz (?) bedeutet: Dort liegt eine Karte, die nicht erkannt wurde. Antippen, um sie einzutragen.",
                                    " An empty spot (?) means: there is a card that was not recognized. Tap it to enter the card.");
    public string CardMissingHere => S("Hier fehlt eine Karte", "A card is missing here");
    public string AddCard => S("Karte hinzufügen", "Add card");
    public string EditCard(int number) => S($"Karte {number} bearbeiten", $"Edit card {number}");
    public string NumberLabel => S("Anzahl", "Number");
    public string ColorLabel => S("Farbe", "Color");
    public string ShadingLabel => S("Füllung", "Shading");
    public string ShapeLabel => S("Form", "Shape");
    public string Remove => S("Entfernen", "Remove");
    public string Done => S("Fertig", "Done");

    // Startseite und Fuß
    public string Intro => S("Fotografiere die ausliegenden Set-Karten. Die App erkennt die Karten direkt auf deinem Gerät " +
                             "und zeigt alle Sets, oder sagt dir, dass drei Karten nachgelegt werden müssen.",
                             "Take a photo of the SET cards on the table. The app recognizes the cards right on your device " +
                             "and shows all sets, or tells you that three more cards have to be added.");
    public string TipSurface => S("Karten auf einen dunklen, einfarbigen Untergrund legen.", "Place the cards on a dark, plain surface.");
    public string TipAbove => S("Möglichst senkrecht von oben fotografieren, ohne Blitz-Spiegelungen.",
                                "Take the photo straight from above if possible, without flash reflections.");
    public string TipNoTouch => S("Die Karten sollten sich nicht berühren.", "The cards should not touch each other.");
    public string EnterManually => S("Karten lieber von Hand eingeben", "Enter the cards by hand instead");
    public string Footer => S("Läuft komplett offline im Browser. Spielregeln: drei Karten bilden ein Set, wenn jede Eigenschaft " +
                              "bei allen dreien gleich oder bei allen dreien verschieden ist.",
                              "Runs completely offline in your browser. Rules: three cards form a set if each property " +
                              "is either the same on all three or different on all three.");
    public string PageNotFound => S("Seite nicht gefunden", "Page not found");
    public string BackToStart => S("Zur Startseite", "Back to the start page");

    // Karteneigenschaften
    public string Name(Number n) => n switch { Number.One => "1", Number.Two => "2", _ => "3" };
    public string Name(Color c) => c switch
    {
        Color.Red => S("Rot", "Red"),
        Color.Green => S("Grün", "Green"),
        _ => S("Lila", "Purple"),
    };
    public string Name(Shading s) => s switch
    {
        Shading.Solid => S("Voll", "Solid"),
        Shading.Striped => S("Gestreift", "Striped"),
        _ => S("Leer", "Open"),
    };
    public string Name(Shape s) => s switch
    {
        Shape.Diamond => S("Raute", "Diamond"),
        Shape.Oval => S("Oval", "Oval"),
        _ => S("Welle", "Squiggle"),
    };
    public string Describe(Card c) => $"{Name(c.Number)} × {Name(c.Color)}, {Name(c.Shading)}, {Name(c.Shape)}";

    /// <summary>Sekunden mit einer Nachkommastelle, auf Deutsch mit Komma (invariante Globalisierung, daher von Hand).</summary>
    public string Seconds(double seconds)
    {
        var text = seconds.ToString("0.0", CultureInfo.InvariantCulture);
        return (_german ? text.Replace('.', ',') : text) + " s";
    }
}
