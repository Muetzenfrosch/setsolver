using Microsoft.JSInterop;

namespace SetGameSolver.Web.Localization;

/// <summary>
/// Aktuelle Sprache der Oberfläche. Beim Start gilt die zuletzt gewählte Sprache, sonst Deutsch, wenn der Browser
/// auf Deutsch eingestellt ist, und andernfalls Englisch (siehe <c>setFinderLanguage</c> in index.html).
/// </summary>
public sealed class LanguageState(IJSRuntime js)
{
    public Texts Texts { get; private set; } = Texts.English;

    public event Action? Changed;

    public async Task InitializeAsync() =>
        Texts = await js.InvokeAsync<string>("setFinderLanguage.get") == "de" ? Texts.German : Texts.English;

    /// <summary>Wechselt die Sprache und merkt sie sich im Browser.</summary>
    public async Task SetAsync(Texts texts)
    {
        if (texts == Texts)
            return;
        Texts = texts;
        Changed?.Invoke();
        await js.InvokeVoidAsync("setFinderLanguage.set", texts.Code);
    }
}
