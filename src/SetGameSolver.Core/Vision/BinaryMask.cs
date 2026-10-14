namespace SetGameSolver.Core.Vision;

/// <summary>Einfache Binärmaske mit morphologischen Operationen und Komponentensuche.</summary>
public sealed class BinaryMask
{
    public BinaryMask(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new bool[width * height];
    }

    public int Width { get; }
    public int Height { get; }
    public bool[] Data { get; }

    public bool this[int x, int y]
    {
        get => Data[y * Width + x];
        set => Data[y * Width + x] = value;
    }

    public int Count() => Data.Count(b => b);

    /// <summary>Erosion mit quadratischem Strukturelement (Radius r), separiert in Zeilen und Spalten.</summary>
    public BinaryMask Erode(int r) => r <= 0 ? this : MinMax(r, erode: true);

    /// <summary>Dilatation mit quadratischem Strukturelement (Radius r).</summary>
    public BinaryMask Dilate(int r) => r <= 0 ? this : MinMax(r, erode: false);

    private BinaryMask MinMax(int r, bool erode)
    {
        // Ein Pixel bleibt bei Erosion gesetzt, wenn im Fenster kein ungesetzter Pixel liegt (Rand zählt als ungesetzt);
        // bei Dilatation wird es gesetzt, wenn im Fenster ein gesetzter Pixel liegt.
        var target = !erode;
        var tmp = new BinaryMask(Width, Height);
        for (var y = 0; y < Height; y++)
        {
            var last = int.MinValue / 2; // letzte Position mit "target"-Wert
            var row = y * Width;
            var next = new int[Width];
            var n = int.MaxValue / 2;
            for (var x = Width - 1; x >= 0; x--)
            {
                if (Data[row + x] == target) n = x;
                next[x] = n;
            }
            for (var x = 0; x < Width; x++)
            {
                if (Data[row + x] == target) last = x;
                var hit = x - last <= r || next[x] - x <= r || (erode && (x - r < 0 || x + r >= Width));
                tmp.Data[row + x] = erode ? !hit : hit;
            }
        }
        var res = new BinaryMask(Width, Height);
        var nextCol = new int[Height];
        for (var x = 0; x < Width; x++)
        {
            var last = int.MinValue / 2;
            var n = int.MaxValue / 2;
            for (var y = Height - 1; y >= 0; y--)
            {
                if (tmp.Data[y * Width + x] == target) n = y;
                nextCol[y] = n;
            }
            for (var y = 0; y < Height; y++)
            {
                if (tmp.Data[y * Width + x] == target) last = y;
                var hit = y - last <= r || nextCol[y] - y <= r || (erode && (y - r < 0 || y + r >= Height));
                res.Data[y * Width + x] = erode ? !hit : hit;
            }
        }
        return res;
    }

    /// <summary>Füllt Löcher: alles, was nicht vom Rand aus über ungesetzte Pixel erreichbar ist, wird gesetzt.</summary>
    public BinaryMask FillHoles()
    {
        var reached = new bool[Data.Length];
        var stack = new Stack<int>();
        void Push(int i)
        {
            if (!Data[i] && !reached[i]) { reached[i] = true; stack.Push(i); }
        }
        for (var x = 0; x < Width; x++) { Push(x); Push((Height - 1) * Width + x); }
        for (var y = 0; y < Height; y++) { Push(y * Width); Push(y * Width + Width - 1); }
        while (stack.Count > 0)
        {
            var i = stack.Pop();
            int x = i % Width, y = i / Width;
            if (x > 0) Push(i - 1);
            if (x < Width - 1) Push(i + 1);
            if (y > 0) Push(i - Width);
            if (y < Height - 1) Push(i + Width);
        }
        var res = new BinaryMask(Width, Height);
        for (var i = 0; i < Data.Length; i++) res.Data[i] = !reached[i];
        return res;
    }

    /// <summary>Zusammenhängende Komponenten (4er-Nachbarschaft).</summary>
    public List<Component> Components(int minArea = 1)
    {
        var labels = new bool[Data.Length];
        var result = new List<Component>();
        var stack = new Stack<int>();
        var pixels = new List<int>();
        for (var start = 0; start < Data.Length; start++)
        {
            if (!Data[start] || labels[start]) continue;
            pixels.Clear();
            labels[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                pixels.Add(i);
                int x = i % Width, y = i / Width;
                if (x > 0 && Data[i - 1] && !labels[i - 1]) { labels[i - 1] = true; stack.Push(i - 1); }
                if (x < Width - 1 && Data[i + 1] && !labels[i + 1]) { labels[i + 1] = true; stack.Push(i + 1); }
                if (y > 0 && Data[i - Width] && !labels[i - Width]) { labels[i - Width] = true; stack.Push(i - Width); }
                if (y < Height - 1 && Data[i + Width] && !labels[i + Width]) { labels[i + Width] = true; stack.Push(i + Width); }
            }
            if (pixels.Count >= minArea) result.Add(new Component(pixels, Width));
        }
        return result;
    }
}

/// <summary>Eine zusammenhängende Pixelregion mit Begrenzungsinformationen.</summary>
public sealed class Component
{
    private readonly int[] _rowMin, _rowMax, _colMin, _colMax;

    internal Component(List<int> pixels, int imageWidth)
    {
        MinX = MinY = int.MaxValue;
        MaxX = MaxY = int.MinValue;
        foreach (var i in pixels)
        {
            int x = i % imageWidth, y = i / imageWidth;
            MinX = Math.Min(MinX, x); MaxX = Math.Max(MaxX, x);
            MinY = Math.Min(MinY, y); MaxY = Math.Max(MaxY, y);
            SumX += x; SumY += y;
        }
        Area = pixels.Count;
        _rowMin = new int[BoundsHeight]; Array.Fill(_rowMin, int.MaxValue);
        _rowMax = new int[BoundsHeight]; Array.Fill(_rowMax, int.MinValue);
        _colMin = new int[BoundsWidth]; Array.Fill(_colMin, int.MaxValue);
        _colMax = new int[BoundsWidth]; Array.Fill(_colMax, int.MinValue);
        Pixels = new int[pixels.Count];
        for (var k = 0; k < pixels.Count; k++)
        {
            var i = pixels[k];
            Pixels[k] = i;
            int x = i % imageWidth - MinX, y = i / imageWidth - MinY;
            _rowMin[y] = Math.Min(_rowMin[y], x); _rowMax[y] = Math.Max(_rowMax[y], x);
            _colMin[x] = Math.Min(_colMin[x], y); _colMax[x] = Math.Max(_colMax[x], y);
        }
    }

    /// <summary>Pixelindizes (y * Breite + x) der Komponente.</summary>
    public int[] Pixels { get; }
    public int Area { get; }
    public int MinX { get; }
    public int MinY { get; }
    public int MaxX { get; }
    public int MaxY { get; }
    public double SumX { get; }
    public double SumY { get; }
    public int BoundsWidth => MaxX - MinX + 1;
    public int BoundsHeight => MaxY - MinY + 1;
    public Point2 Centroid => new(SumX / Area, SumY / Area);

    /// <summary>
    /// Randpunkte: pro Zeile und Spalte die äußersten Pixel (Pixelränder, daher +1 auf der Max-Seite).
    /// Für konvexe Formen ist das die vollständige Außenkontur.
    /// </summary>
    public List<Point2> OuterPoints()
    {
        var pts = new List<Point2>();
        for (var y = 0; y < BoundsHeight; y++)
        {
            if (_rowMin[y] == int.MaxValue) continue;
            pts.Add(new Point2(MinX + _rowMin[y], MinY + y + 0.5));
            pts.Add(new Point2(MinX + _rowMax[y] + 1, MinY + y + 0.5));
        }
        for (var x = 0; x < BoundsWidth; x++)
        {
            if (_colMin[x] == int.MaxValue) continue;
            pts.Add(new Point2(MinX + x + 0.5, MinY + _colMin[x]));
            pts.Add(new Point2(MinX + x + 0.5, MinY + _colMax[x] + 1));
        }
        return pts;
    }

    public List<Point2> Hull() => Geometry.ConvexHull(OuterPoints());
}
