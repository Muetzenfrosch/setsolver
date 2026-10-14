using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SetGameSolver.Core.Model;
using SetGameSolver.Core.Solving;
using SetGameSolver.Core.Vision;

namespace SetGameSolver.Web.Pages;

public partial class Home : IAsyncDisposable
{
    /// <summary>Fotos werden im Browser auf diese Kantenlänge verkleinert; das genügt und hält die Rechenzeit klein.</summary>
    private const int MaxSide = 1280;

    private sealed record PhotoInfo(int Width, int Height, string Url);

    private sealed class Slot(Card card, Quad? quad, bool uncertain)
    {
        public Card Card { get; set; } = card;
        public Quad? Quad { get; } = quad;
        public bool Uncertain { get; } = uncertain;
        public bool Edited { get; set; }
    }

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IJSObjectReference? _module;
    private ElementReference _cameraInput;
    private ElementReference _fileInput;

    private PhotoInfo? _photo;
    private readonly List<Slot> _slots = [];
    private SolveResult? _result;
    private int _selectedSet;
    private int? _editing;
    private bool _busy;
    private bool _manual;
    private string? _error;
    private string? _elapsed;

    private FoundSet? SelectedSet =>
        _result is { HasSet: true } r && _selectedSet < r.Sets.Count ? r.Sets[_selectedSet] : null;

    private int UncertainCount => _slots.Count(s => s.Uncertain && !s.Edited);

    private double LabelRadius => _photo is null ? 10 : Math.Max(_photo.Width, _photo.Height) / 60.0;

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/image-loader.js");

    private async Task LoadFromInputAsync(ElementReference input)
    {
        var module = await ModuleAsync();
        await RunAsync(() => module.InvokeAsync<PhotoInfo?>("loadFromInput", input, MaxSide));
    }

    private async Task LoadSampleAsync()
    {
        var module = await ModuleAsync();
        var sample = Random.Shared.Next(2) == 0 ? "sample/auslage-mit-sets.jpg" : "sample/auslage-ohne-set.jpg";
        await RunAsync(() => module.InvokeAsync<PhotoInfo?>("loadFromUrl", sample, MaxSide));
    }

    private async Task RunAsync(Func<ValueTask<PhotoInfo?>> load)
    {
        _error = null;
        _editing = null;
        _manual = false;
        try
        {
            var photo = await load();
            if (photo is null)
                return;

            _photo = photo;
            _slots.Clear();
            _result = null;
            _busy = true;
            StateHasChanged();

            var module = await ModuleAsync();
            await module.InvokeVoidAsync("nextFrame");
            var pixels = await module.InvokeAsync<byte[]>("takePixels");

            var watch = Stopwatch.StartNew();
            var detected = SetRecognizer.Recognize(new RgbaImage(photo.Width, photo.Height, pixels));
            watch.Stop();

            _slots.AddRange(detected.Select(d => new Slot(d.Card, d.Quad, d.Uncertain)));
            // Invariante Globalisierung (kleinerer Download): Dezimalkomma von Hand setzen.
            _elapsed = watch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " s";
            Solve();
        }
        catch (Exception ex)
        {
            _error = $"Das Bild konnte nicht ausgewertet werden: {ex.Message}";
        }
        finally
        {
            _busy = false;
        }
    }

    private void Solve()
    {
        _result = SetFinder.Solve(_slots.Select(s => s.Card).ToArray());
        if (_selectedSet >= _result.Sets.Count)
            _selectedSet = 0;
    }

    private void Edit(int index) => _editing = _editing == index ? null : index;

    private void Update(int index, Card card)
    {
        _slots[index].Card = card;
        _slots[index].Edited = true;
        Solve();
    }

    private void Remove(int index)
    {
        _slots.RemoveAt(index);
        _editing = null;
        Solve();
    }

    private void AddCard()
    {
        // Eine Karte vorschlagen, die noch nicht ausliegt.
        var used = _slots.Select(s => s.Card).ToHashSet();
        var card = Card.AllCards.FirstOrDefault(c => !used.Contains(c));
        _slots.Add(new Slot(card, null, false) { Edited = true });
        _editing = _slots.Count - 1;
        Solve();
    }

    private void StartManual()
    {
        _manual = true;
        _photo = null;
        _slots.Clear();
        _elapsed = null;
        Solve();
    }

    private static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Points(Quad q) => string.Join(" ", q.Corners.Select(p => $"{F(p.X)},{F(p.Y)}"));

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
