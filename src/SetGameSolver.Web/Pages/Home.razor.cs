using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SetGameSolver.Core.Model;
using SetGameSolver.Core.Solving;
using SetGameSolver.Core.Synthetic;
using SetGameSolver.Core.Vision;
using SetGameSolver.Web.Localization;

namespace SetGameSolver.Web.Pages;

public partial class Home : IAsyncDisposable
{
    /// <summary>Fotos werden im Browser auf diese Kantenlänge verkleinert; das genügt und hält die Rechenzeit klein.</summary>
    private const int MaxSide = 1280;

    private sealed record PhotoInfo(int Width, int Height, string Url);

    private sealed class Slot(Card card, Quad? quad, bool uncertain, GridCell? cell)
    {
        public Card Card { get; set; } = card;
        public Quad? Quad { get; } = quad;
        public bool Uncertain { get; } = uncertain;
        public bool Edited { get; set; }
        /// <summary>Platz im Raster der Auslage (nur wenn die Karten aus einem Foto stammen).</summary>
        public GridCell? Cell { get; } = cell;
    }

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [CascadingParameter] public Texts T { get; set; } = Texts.English;

    private IJSObjectReference? _module;
    private ElementReference _cameraInput;
    private ElementReference _fileInput;

    private PhotoInfo? _photo;
    private readonly List<Slot> _slots = [];
    private CardGrid? _grid; // Raster der Auslage auf dem Foto; null: Karten einfach der Reihe nach zeigen
    private SolveResult? _result;
    private int _selectedSet;
    private int? _editing;
    private bool _busy;
    private bool _manual;
    private string? _error; // Grund, warum das Bild nicht ausgewertet werden konnte
    private double? _elapsed; // Rechenzeit der Erkennung in Sekunden

    private FoundSet? SelectedSet =>
        _result is { HasSet: true } r && _selectedSet < r.Sets.Count ? r.Sets[_selectedSet] : null;

    private int UncertainCount => _slots.Count(s => s.Uncertain && !s.Edited);

    private int GridRows => Math.Max(_grid?.Rows ?? 0, _slots.Max(s => s.Cell?.Row + 1) ?? 0);
    private int GridColumns => Math.Max(_grid?.Columns ?? 0, _slots.Max(s => s.Cell?.Column + 1) ?? 0);
    private bool HasFreeCells => _grid is not null &&
        _slots.Count(s => s.Cell is { } c && c.Row < _grid.Rows && c.Column < _grid.Columns) < _grid.Rows * _grid.Columns;

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
        // Jedes Mal 12 neue Karten; der Browser zeichnet das Bild, danach läuft die normale Erkennung.
        var sample = SampleScene.Create(Random.Shared);
        await RunAsync(() => module.InvokeAsync<PhotoInfo?>("loadFromSvg", sample.Svg, MaxSide));
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
            _grid = null;
            _result = null;
            _busy = true;
            StateHasChanged();

            var module = await ModuleAsync();
            await module.InvokeVoidAsync("nextFrame");
            var pixels = await module.InvokeAsync<byte[]>("takePixels");

            var watch = Stopwatch.StartNew();
            var detected = SetRecognizer.Recognize(new RgbaImage(photo.Width, photo.Height, pixels));
            watch.Stop();

            // Die Karten kommen schon zeilenweise sortiert; das Raster bestimmt zusätzlich die freien Plätze.
            if (detected.Count > 0)
                _grid = CardLayout.Arrange(detected.Select(d => d.Quad).ToList());
            _slots.AddRange(detected.Select((d, i) => new Slot(d.Card, d.Quad, d.Uncertain, _grid?.Cells[i])));
            _elapsed = watch.Elapsed.TotalSeconds;
            Solve();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
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

    /// <summary>
    /// Fügt eine Karte hinzu: auf dem angetippten freien Platz, sonst im Raster hinter der letzten Karte.
    /// </summary>
    private void AddCard(GridCell? cell = null)
    {
        // Eine Karte vorschlagen, die noch nicht ausliegt.
        var used = _slots.Select(s => s.Card).ToHashSet();
        var card = Card.AllCards.FirstOrDefault(c => !used.Contains(c));
        if (_grid is not null && cell is null)
        {
            var columns = GridColumns;
            var next = _slots.Select(s => s.Cell is { } c ? c.Row * columns + c.Column + 1 : 0).DefaultIfEmpty(0).Max();
            cell = new GridCell(next / columns, next % columns);
        }
        var slot = new Slot(card, null, false, cell) { Edited = true };
        _slots.Add(slot);
        if (_grid is not null)
        {
            // Nummern zeilenweise wie im Raster.
            var ordered = _slots.OrderBy(s => s.Cell?.Row).ThenBy(s => s.Cell?.Column).ToList();
            _slots.Clear();
            _slots.AddRange(ordered);
        }
        _editing = _slots.IndexOf(slot);
        Solve();
    }

    private void StartManual()
    {
        _manual = true;
        _photo = null;
        _slots.Clear();
        _grid = null;
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
