using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using OmniExplorer.Core;

namespace OmniExplorer.Desktop.Rendering;

public sealed class GraphSceneControl : Control
{
    private readonly RadialGraphLayout _layoutEngine = new();
    private readonly DispatcherTimer _animationTimer;
    private readonly Stopwatch _animationClock = new();
    private readonly Dictionary<string, Rect> _hitTargets = new(StringComparer.OrdinalIgnoreCase);
    private ExplorerNeighborhood? _neighborhood;
    private IReadOnlyDictionary<string, GraphLayoutNode> _targetLayout =
        new Dictionary<string, GraphLayoutNode>();
    private Dictionary<string, GraphLayoutNode> _animationFrom = new();
    private IReadOnlySet<string> _highlights = new HashSet<string>();
    private string? _selectedNodeId;
    private Vector _pan;
    private Point _lastPointer;
    private bool _isPanning;
    private double _zoom = 1;

    public GraphSceneControl()
    {
        Focusable = true;
        ClipToBounds = true;
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += (_, _) =>
        {
            if (_animationClock.ElapsedMilliseconds >= 280)
            {
                _animationTimer.Stop();
                _animationClock.Stop();
            }

            InvalidateVisual();
        };
    }

    public event EventHandler<string>? NodeSelected;

    public event EventHandler<string>? NodeActivated;

    public event EventHandler? BackRequested;

    public void SetScene(
        ExplorerNeighborhood? neighborhood,
        string? selectedNodeId,
        IReadOnlySet<string>? highlights,
        bool animate = true)
    {
        var previousLayout = CurrentLayout();
        _neighborhood = neighborhood;
        _selectedNodeId = selectedNodeId;
        _highlights = highlights ?? new HashSet<string>();
        _targetLayout = neighborhood is null
            ? new Dictionary<string, GraphLayoutNode>()
            : _layoutEngine.Layout(neighborhood);

        _animationFrom = _targetLayout.ToDictionary(
            pair => pair.Key,
            pair => previousLayout.TryGetValue(pair.Key, out var previous)
                ? previous
                : pair.Value with { X = 0, Y = 0, Scale = 0.25, Opacity = 0 });

        if (animate && neighborhood is not null)
        {
            _animationClock.Restart();
            _animationTimer.Start();
        }
        else
        {
            _animationClock.Reset();
            _animationTimer.Stop();
        }

        InvalidateVisual();
    }

    public void ZoomIn() => SetZoom(_zoom * 1.15);

    public void ZoomOut() => SetZoom(_zoom / 1.15);

    public void ResetView()
    {
        _zoom = 1;
        _pan = default;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var palette = ActualThemeVariant == ThemeVariant.Light ? ScenePalette.Light : ScenePalette.Dark;
        context.DrawRectangle(new SolidColorBrush(palette.Background), null, Bounds);
        DrawBackgroundNetwork(context, palette);

        if (_neighborhood is null)
        {
            return;
        }

        var layout = CurrentLayout();
        _hitTargets.Clear();
        DrawEdges(context, palette, layout);

        foreach (var node in _neighborhood.Nodes
                     .OrderBy(node => node.Id == _neighborhood.FocusNodeId ? 1 : 0))
        {
            if (!layout.TryGetValue(node.Id, out var position))
            {
                continue;
            }

            DrawNode(context, palette, node, position);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetPosition(this);
        var nodeId = HitTest(point);
        if (nodeId is not null)
        {
            _selectedNodeId = nodeId;
            NodeSelected?.Invoke(this, nodeId);
            if (e.ClickCount >= 2)
            {
                NodeActivated?.Invoke(this, nodeId);
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
            _lastPointer = point;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isPanning)
        {
            return;
        }

        var point = e.GetPosition(this);
        _pan += point - _lastPointer;
        _lastPointer = point;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        SetZoom(_zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_neighborhood is null)
        {
            return;
        }

        if (e.Key == Key.Back)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && _selectedNodeId is not null)
        {
            NodeActivated?.Invoke(this, _selectedNodeId);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var nodes = _neighborhood.Nodes.Where(node => node.Kind != ExplorerNodeKind.Aggregate).ToArray();
            var selectedIndex = Array.FindIndex(nodes, node =>
                StringComparer.OrdinalIgnoreCase.Equals(node.Id, _selectedNodeId));
            var direction = e.Key is Key.Left or Key.Up ? -1 : 1;
            selectedIndex = (selectedIndex + direction + nodes.Length) % nodes.Length;
            _selectedNodeId = nodes[selectedIndex].Id;
            NodeSelected?.Invoke(this, _selectedNodeId);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void DrawBackgroundNetwork(DrawingContext context, ScenePalette palette)
    {
        var pointBrush = new SolidColorBrush(WithAlpha(palette.BackgroundPoint, 34));
        var linePen = new Pen(new SolidColorBrush(WithAlpha(palette.BackgroundPoint, 18)), 0.7);
        var points = new Point[34];

        for (var index = 0; index < points.Length; index++)
        {
            var x = ((index * 127) % 997) / 997d * Bounds.Width;
            var y = ((index * 283 + 71) % 991) / 991d * Bounds.Height;
            points[index] = new Point(x, y);
            context.DrawEllipse(pointBrush, null, points[index], 1.2, 1.2);
            if (index > 2 && index % 2 == 0)
            {
                context.DrawLine(linePen, points[index - 2], points[index]);
            }
        }
    }

    private void DrawEdges(
        DrawingContext context,
        ScenePalette palette,
        IReadOnlyDictionary<string, GraphLayoutNode> layout)
    {
        foreach (var edge in _neighborhood!.Edges)
        {
            if (!layout.TryGetValue(edge.SourceId, out var source) ||
                !layout.TryGetValue(edge.TargetId, out var target))
            {
                continue;
            }

            var start = ToCanvas(source);
            var end = ToCanvas(target);
            var opacity = Math.Min(source.Opacity, target.Opacity);
            context.DrawLine(
                new Pen(new SolidColorBrush(WithAlpha(palette.EdgeGlow, (byte)(32 * opacity))), 5),
                start,
                end);
            context.DrawLine(
                new Pen(new SolidColorBrush(WithAlpha(palette.Edge, (byte)(180 * opacity))), 1.15),
                start,
                end);
            context.DrawEllipse(
                new SolidColorBrush(WithAlpha(palette.EdgeGlow, (byte)(210 * opacity))),
                null,
                end,
                2.2,
                2.2);
        }
    }

    private void DrawNode(
        DrawingContext context,
        ScenePalette palette,
        ExplorerNode node,
        GraphLayoutNode layout)
    {
        var center = ToCanvas(layout);
        var scale = layout.Scale * Math.Clamp(_zoom, 0.72, 1.6);
        var isFocus = node.Id == _neighborhood!.FocusNodeId;
        var isSelected = StringComparer.OrdinalIgnoreCase.Equals(node.Id, _selectedNodeId);
        var isHighlighted = _highlights.Contains(node.Id);
        var opacity = Math.Clamp(layout.Opacity, 0, 1);
        var color = isHighlighted
            ? palette.Search
            : isFocus
                ? palette.Focus
                : palette.Node;
        var stroke = new Pen(new SolidColorBrush(WithAlpha(color, (byte)(245 * opacity))), isFocus ? 1.8 : 1.2);
        var halfWidth = 25 * scale;
        var halfHeight = 19 * scale;
        var hitRect = new Rect(center.X - halfWidth - 10, center.Y - halfHeight - 9, (halfWidth + 10) * 2, (halfHeight + 26) * 2);
        _hitTargets[node.Id] = hitRect;

        if (isFocus || isSelected || isHighlighted)
        {
            var halo = isHighlighted ? palette.Search : palette.Selection;
            context.DrawEllipse(
                null,
                new Pen(new SolidColorBrush(WithAlpha(halo, (byte)(65 * opacity))), isFocus ? 10 : 6),
                center,
                halfWidth + 12,
                halfHeight + 12);
        }

        switch (node.Kind)
        {
            case ExplorerNodeKind.Folder:
            case ExplorerNodeKind.Context:
                DrawFolder(context, center, halfWidth, halfHeight, stroke);
                break;
            case ExplorerNodeKind.File:
                DrawFile(context, center, halfWidth * 0.72, halfHeight, stroke);
                break;
            case ExplorerNodeKind.Aggregate:
                context.DrawEllipse(null, stroke, center, halfHeight, halfHeight);
                context.DrawEllipse(new SolidColorBrush(WithAlpha(color, 170)), null, center, 2.5, 2.5);
                break;
        }

        var shouldDrawLabel = isFocus ||
            isSelected ||
            isHighlighted ||
            node.Kind == ExplorerNodeKind.Aggregate ||
            layout.Scale * _zoom >= 0.7;
        if (!shouldDrawLabel)
        {
            return;
        }

        var labelBrush = new SolidColorBrush(WithAlpha(isFocus ? palette.Text : color, (byte)(255 * opacity)));
        var text = new FormattedText(
            node.Name,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            isFocus ? 14 : Math.Max(10, 11.5 * scale),
            labelBrush)
        {
            MaxTextWidth = isFocus ? 210 : 150,
            TextAlignment = TextAlignment.Center,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        context.DrawText(text, new Point(center.X - (text.MaxTextWidth / 2), center.Y + halfHeight + 7));
    }

    private static void DrawFolder(
        DrawingContext context,
        Point center,
        double halfWidth,
        double halfHeight,
        Pen stroke)
    {
        var body = new Rect(
            center.X - halfWidth,
            center.Y - halfHeight + 4,
            halfWidth * 2,
            (halfHeight * 2) - 4);
        var tab = new Rect(
            center.X - halfWidth + 3,
            center.Y - halfHeight - 2,
            halfWidth * 0.78,
            8);
        context.DrawRectangle(null, stroke, body, 3, 3);
        context.DrawRectangle(null, stroke, tab, 2, 2);
    }

    private static void DrawFile(
        DrawingContext context,
        Point center,
        double halfWidth,
        double halfHeight,
        Pen stroke)
    {
        var body = new Rect(center.X - halfWidth, center.Y - halfHeight, halfWidth * 2, halfHeight * 2);
        context.DrawRectangle(null, stroke, body, 2, 2);
        context.DrawLine(
            stroke,
            new Point(center.X - (halfWidth * 0.55), center.Y),
            new Point(center.X + (halfWidth * 0.55), center.Y));
    }

    private IReadOnlyDictionary<string, GraphLayoutNode> CurrentLayout()
    {
        if (!_animationClock.IsRunning || _animationClock.ElapsedMilliseconds >= 280)
        {
            return _targetLayout;
        }

        var raw = Math.Clamp(_animationClock.Elapsed.TotalMilliseconds / 280d, 0, 1);
        var amount = 1 - Math.Pow(1 - raw, 3);
        return _targetLayout.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                var from = _animationFrom.TryGetValue(pair.Key, out var source)
                    ? source
                    : pair.Value;
                var target = pair.Value;
                return new GraphLayoutNode(
                    pair.Key,
                    Lerp(from.X, target.X, amount),
                    Lerp(from.Y, target.Y, amount),
                    Lerp(from.Scale, target.Scale, amount),
                    Lerp(from.Opacity, target.Opacity, amount));
            });
    }

    private Point ToCanvas(GraphLayoutNode node)
    {
        var width = Math.Max(1, Bounds.Width * 0.92) * _zoom;
        var height = Math.Max(1, Bounds.Height * 0.9) * _zoom;
        return new Point(
            (Bounds.Width / 2) + _pan.X + (node.X * width),
            (Bounds.Height / 2) + _pan.Y + (node.Y * height));
    }

    private string? HitTest(Point point) => _hitTargets
        .Where(pair => pair.Value.Contains(point))
        .OrderBy(pair => pair.Value.Width * pair.Value.Height)
        .Select(pair => pair.Key)
        .FirstOrDefault();

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.5, 2.4);
        InvalidateVisual();
    }

    private static double Lerp(double from, double to, double amount) =>
        from + ((to - from) * amount);

    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);
}
