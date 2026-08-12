using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using OmniBrille.Core;

namespace OmniBrille.Desktop.Rendering;

public sealed record SceneDiagnostics(
    int Nodes,
    int Edges,
    int Labels,
    int Budget,
    double Zoom,
    double TextScale,
    bool AnimationActive,
    TimeSpan LayoutDuration,
    TimeSpan ScenePreparationDuration,
    TimeSpan LastRenderDuration,
    TimeSpan BackgroundDuration,
    TimeSpan EdgeDuration,
    TimeSpan GlyphDuration,
    TimeSpan LabelPreparationDuration,
    TimeSpan LabelCollisionDuration,
    TimeSpan LabelDrawDuration,
    long RenderAllocatedBytes,
    int TextCacheEntries,
    int ResourceCacheEntries);

public sealed class GraphSceneControl : Control
{
    private const double AnimationDurationMilliseconds = 440;

    private readonly RadialGraphLayout _layoutEngine = new();
    private readonly DispatcherTimer _animationTimer;
    private readonly Stopwatch _animationClock = new();
    private readonly Dictionary<string, Rect> _hitTargets = new(ExplorerIdentity.Comparer);
    private readonly BoundedLruCache<LabelTextKey, FormattedText> _textCache = new(256);
    private readonly BoundedLruCache<Color, SolidColorBrush> _brushCache = new(192);
    private readonly BoundedLruCache<PenKey, Pen> _penCache = new(384);
    private ExplorerNeighborhood? _neighborhood;
    private IReadOnlyDictionary<string, GraphLayoutNode> _targetLayout =
        new Dictionary<string, GraphLayoutNode>();
    private Dictionary<string, GraphLayoutNode> _animationFrom = new();
    private IReadOnlySet<string> _highlights = new HashSet<string>();
    private string? _selectedNodeId;
    private string? _hoveredNodeId;
    private Vector _pan;
    private Point _lastPointer;
    private bool _isPanning;
    private double _zoom = 1;
    private double _textScale = 1;
    private TimeSpan _layoutDuration;
    private TimeSpan _scenePreparationDuration;
    private TimeSpan _lastRenderDuration;
    private TimeSpan _backgroundDuration;
    private TimeSpan _edgeDuration;
    private TimeSpan _glyphDuration;
    private TimeSpan _labelPreparationDuration;
    private TimeSpan _labelCollisionDuration;
    private TimeSpan _labelDrawDuration;
    private long _renderAllocatedBytes;
    private int _renderedLabelCount;
    private ScenePalette? _cachedPalette;
    private double _cachedRenderScaling;

    public GraphSceneControl()
    {
        Focusable = true;
        ClipToBounds = true;
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += (_, _) =>
        {
            if (_animationClock.Elapsed.TotalMilliseconds >= AnimationDurationMilliseconds)
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

    public event EventHandler? DismissRequested;

    public bool ReducedMotion { get; set; }

    public bool ReducedEffects { get; set; }

    public bool SearchActive { get; set; }

    public double TextScale
    {
        get => _textScale;
        set
        {
            var normalized = Math.Clamp(value, 1, 2);
            if (Math.Abs(_textScale - normalized) < 0.001)
            {
                return;
            }

            _textScale = normalized;
            _textCache.Clear();
            InvalidateVisual();
        }
    }

    public SceneDiagnostics Diagnostics => new(
        _neighborhood?.Nodes.Count ?? 0,
        _neighborhood?.Edges.Count ?? 0,
        _renderedLabelCount,
        GraphNeighborhoodBuilder.DefaultNodeBudget,
        _zoom,
        _textScale,
        _animationClock.IsRunning,
        _layoutDuration,
        _scenePreparationDuration,
        _lastRenderDuration,
        _backgroundDuration,
        _edgeDuration,
        _glyphDuration,
        _labelPreparationDuration,
        _labelCollisionDuration,
        _labelDrawDuration,
        _renderAllocatedBytes,
        _textCache.Count,
        _brushCache.Count + _penCache.Count);

    public void SetScene(
        ExplorerNeighborhood? neighborhood,
        string? selectedNodeId,
        IReadOnlySet<string>? highlights,
        bool animate = true)
    {
        var preparationClock = Stopwatch.StartNew();
        var previousLayout = CurrentLayout();
        _neighborhood = neighborhood;
        _selectedNodeId = selectedNodeId;
        _highlights = highlights ?? new HashSet<string>();

        var layoutClock = Stopwatch.StartNew();
        _targetLayout = neighborhood is null
            ? new Dictionary<string, GraphLayoutNode>()
            : _layoutEngine.Layout(neighborhood, previousLayout);
        layoutClock.Stop();
        _layoutDuration = layoutClock.Elapsed;

        _animationFrom = _targetLayout.ToDictionary(
            pair => pair.Key,
            pair => previousLayout.TryGetValue(pair.Key, out var previous)
                ? previous
                : pair.Value with
                {
                    X = 0,
                    Y = 0,
                    Scale = Math.Min(0.28, pair.Value.Scale),
                    Opacity = 0,
                });

        if (animate && !ReducedMotion && neighborhood is not null)
        {
            _animationClock.Restart();
            _animationTimer.Start();
        }
        else
        {
            _animationClock.Reset();
            _animationTimer.Stop();
        }

        UpdateAutomationDescription();
        NotifyAutomationSceneChanged();
        preparationClock.Stop();
        _scenePreparationDuration = preparationClock.Elapsed;
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
        var renderClock = Stopwatch.StartNew();
        var allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
        _labelPreparationDuration = TimeSpan.Zero;
        base.Render(context);
        var palette = ActualThemeVariant == ThemeVariant.Light ? ScenePalette.Light : ScenePalette.Dark;
        EnsureCacheContext(palette);
        var phaseClock = Stopwatch.StartNew();
        context.DrawRectangle(Brush(palette.Background), null, Bounds);
        DrawBackgroundNetwork(context, palette);
        phaseClock.Stop();
        _backgroundDuration = phaseClock.Elapsed;

        if (_neighborhood is null)
        {
            _renderedLabelCount = 0;
            renderClock.Stop();
            _lastRenderDuration = renderClock.Elapsed;
            _renderAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore;
            return;
        }

        var layout = CurrentLayout();
        var presentationContext = new GraphPresentationContext(
            _zoom,
            _neighborhood.Nodes.Count,
            _neighborhood.FocusNodeId,
            _selectedNodeId,
            _hoveredNodeId,
            _highlights,
            SearchActive,
            ReducedEffects,
            _textScale);
        var presentations = _neighborhood.Nodes.ToDictionary(
            node => node.Id,
            node => GraphPresentationPolicy.Evaluate(node, layout[node.Id], presentationContext),
            ExplorerIdentity.Comparer);

        _hitTargets.Clear();
        phaseClock.Restart();
        DrawEdges(context, palette, layout, presentations);
        phaseClock.Stop();
        _edgeDuration = phaseClock.Elapsed;

        var labels = new List<PreparedLabel>();
        phaseClock.Restart();
        foreach (var node in _neighborhood.Nodes
                     .OrderBy(node => layout[node.Id].Depth)
                     .ThenBy(node => node.Id == _neighborhood.FocusNodeId ? 1 : 0))
        {
            if (!layout.TryGetValue(node.Id, out var position))
            {
                continue;
            }

            var preparedLabel = DrawNodeGlyph(context, palette, node, position, presentations[node.Id]);
            if (preparedLabel is not null)
            {
                labels.Add(preparedLabel);
            }
        }
        phaseClock.Stop();
        _glyphDuration = phaseClock.Elapsed - _labelPreparationDuration;

        var labelBudget = GraphPresentationPolicy.RecommendedLabelBudget(_zoom, _neighborhood.Nodes.Count, _textScale);
        phaseClock.Restart();
        var visibleLabels = GraphPresentationPolicy.ResolveLabels(
            labels.Select(label => label.Candidate),
            labelBudget,
            padding: ReducedEffects ? 3 : 5);
        phaseClock.Stop();
        _labelCollisionDuration = phaseClock.Elapsed;
        phaseClock.Restart();
        foreach (var label in labels.Where(item => visibleLabels.Contains(item.Candidate.NodeId)))
        {
            using (context.PushOpacity(label.Opacity))
            {
                context.DrawText(label.Text, label.Origin);
            }
        }
        phaseClock.Stop();
        _labelDrawDuration = phaseClock.Elapsed;

        _renderedLabelCount = visibleLabels.Count;
        renderClock.Stop();
        _lastRenderDuration = renderClock.Elapsed;
        _renderAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore;
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

            UpdateAutomationDescription();
            NotifyAutomationSelectionChanged();
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

    protected override AutomationPeer OnCreateAutomationPeer() => new GraphSceneAutomationPeer(this);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_isPanning)
        {
            _pan += point - _lastPointer;
            _lastPointer = point;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var hovered = HitTest(point);
        if (!ExplorerIdentity.Equals(_hoveredNodeId, hovered))
        {
            _hoveredNodeId = hovered;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredNodeId is not null)
        {
            _hoveredNodeId = null;
            InvalidateVisual();
        }
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
        if (e.Key == Key.Escape)
        {
            DismissRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemPlus || e.Key == Key.Add)
        {
            ZoomIn();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
        {
            ZoomOut();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.D0 || e.Key == Key.NumPad0)
        {
            ResetView();
            e.Handled = true;
            return;
        }

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
            var nodes = _neighborhood.Nodes.Where(node => node.IsNavigable || node.Kind != ExplorerNodeKind.Aggregate).ToArray();
            var selectedIndex = Array.FindIndex(nodes, node =>
                ExplorerIdentity.Equals(node.Id, _selectedNodeId));
            var direction = e.Key is Key.Left or Key.Up ? -1 : 1;
            selectedIndex = (selectedIndex + direction + nodes.Length) % nodes.Length;
            _selectedNodeId = nodes[selectedIndex].Id;
            NodeSelected?.Invoke(this, _selectedNodeId);
            UpdateAutomationDescription();
            NotifyAutomationSelectionChanged();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    internal IReadOnlyList<ExplorerNode> GetAutomationNodes() => _neighborhood?.Nodes ?? [];

    internal ExplorerNode? GetAutomationNode(string nodeId) => _neighborhood?.Nodes.FirstOrDefault(node =>
        ExplorerIdentity.Equals(node.Id, nodeId));

    internal bool IsAutomationNodeSelected(string nodeId) =>
        ExplorerIdentity.Equals(nodeId, _selectedNodeId);

    internal bool IsAutomationNodeFocused(string nodeId) =>
        ExplorerIdentity.Equals(nodeId, _neighborhood?.FocusNodeId);

    internal Rect GetAutomationNodeBounds(string nodeId) =>
        _hitTargets.TryGetValue(nodeId, out var bounds) ? bounds : default;

    internal void SelectAutomationNode(string nodeId)
    {
        if (GetAutomationNode(nodeId) is null)
        {
            return;
        }

        Focus();
        _selectedNodeId = nodeId;
        NodeSelected?.Invoke(this, nodeId);
        UpdateAutomationDescription();
        NotifyAutomationSelectionChanged();
        InvalidateVisual();
    }

    internal void ActivateAutomationNode(string nodeId)
    {
        SelectAutomationNode(nodeId);
        NodeActivated?.Invoke(this, nodeId);
    }

    private void DrawBackgroundNetwork(DrawingContext context, ScenePalette palette)
    {
        var density = ReducedEffects ? 16 : 42;
        var pointBrush = Brush(palette.BackgroundPoint, ReducedEffects ? (byte)18 : (byte)38);
        var linePen = Pen(palette.BackgroundPoint, ReducedEffects ? (byte)8 : (byte)20, 0.65);
        var trianglePen = Pen(palette.BackgroundPoint, ReducedEffects ? (byte)5 : (byte)12, 0.55);
        var points = new Point[density];

        for (var index = 0; index < points.Length; index++)
        {
            var x = ((index * 127) % 997) / 997d * Bounds.Width;
            var y = ((index * 283 + 71) % 991) / 991d * Bounds.Height;
            points[index] = new Point(x, y);
            context.DrawEllipse(pointBrush, null, points[index], index % 7 == 0 ? 1.8 : 1.05, index % 7 == 0 ? 1.8 : 1.05);
            if (index > 2 && index % 2 == 0)
            {
                context.DrawLine(linePen, points[index - 2], points[index]);
            }

            if (!ReducedEffects && index > 4 && index % 5 == 0)
            {
                context.DrawLine(trianglePen, points[index - 5], points[index - 2]);
                context.DrawLine(trianglePen, points[index - 2], points[index]);
            }
        }
    }

    private void DrawEdges(
        DrawingContext context,
        ScenePalette palette,
        IReadOnlyDictionary<string, GraphLayoutNode> layout,
        Dictionary<string, GraphNodePresentation> presentations)
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
            var presentation = presentations[edge.TargetId];
            var opacity = Math.Min(source.Opacity, target.Opacity) * presentation.OpacityMultiplier;
            if (!ReducedEffects)
            {
                context.DrawLine(
                    Pen(palette.EdgeGlow, ToByte(28 * opacity * presentation.GlowMultiplier), 4.5),
                    start,
                    end);
            }

            context.DrawLine(
                Pen(
                    palette.Edge,
                    ToByte(190 * opacity * presentation.EdgeMultiplier),
                    target.Depth == 1 ? 1.25 : 0.82),
                start,
                end);
            context.DrawEllipse(
                Brush(palette.EdgeGlow, ToByte(220 * opacity)),
                null,
                end,
                target.Depth == 1 ? 2.1 : 1.25,
                target.Depth == 1 ? 2.1 : 1.25);
        }
    }

    private PreparedLabel? DrawNodeGlyph(
        DrawingContext context,
        ScenePalette palette,
        ExplorerNode node,
        GraphLayoutNode layout,
        GraphNodePresentation presentation)
    {
        var center = ToCanvas(layout);
        var scale = layout.Scale * Math.Clamp(_zoom, 0.68, 1.65);
        var isFocus = node.Id == _neighborhood!.FocusNodeId;
        var isSelected = ExplorerIdentity.Equals(node.Id, _selectedNodeId);
        var isHovered = ExplorerIdentity.Equals(node.Id, _hoveredNodeId);
        var isHighlighted = _highlights.Contains(node.Id);
        var opacity = Math.Clamp(layout.Opacity * presentation.OpacityMultiplier, 0, 1);
        var color = isHighlighted
            ? palette.Search
            : isFocus
                ? palette.Focus
                : node.Kind == ExplorerNodeKind.Context
                    ? palette.Context
                    : palette.Node;

        if (presentation.LevelOfDetail == GraphLevelOfDetail.Point)
        {
            context.DrawEllipse(Brush(color, ToByte(220 * opacity)), null, center, 2.2, 2.2);
            _hitTargets[node.Id] = new Rect(center.X - 8, center.Y - 8, 16, 16);
            return null;
        }

        var stroke = Pen(
            color,
            ToByte(250 * opacity),
            isFocus ? 2.05 : layout.Depth == 1 ? 1.25 : 0.95);
        var halfWidth = 25 * scale;
        var halfHeight = 19 * scale;
        _hitTargets[node.Id] = new Rect(
            center.X - halfWidth - 10,
            center.Y - halfHeight - 9,
            (halfWidth + 10) * 2,
            (halfHeight + 26) * 2);

        if (isFocus || isSelected || isHighlighted || isHovered)
        {
            var halo = isHighlighted ? palette.Search : palette.Selection;
            var haloAlpha = ReducedEffects ? 38 : isFocus ? 82 : 58;
            context.DrawEllipse(
                null,
                Pen(halo, ToByte(haloAlpha * opacity), ReducedEffects ? 4 : isFocus ? 11 : 7),
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
                DrawAggregate(context, center, halfHeight, stroke, Brush(color, 185), node.IsNavigable);
                break;
        }

        if (presentation.LevelOfDetail < GraphLevelOfDetail.Labeled)
        {
            return null;
        }

        var fontSize = (isFocus ? 14.5 : Math.Clamp(11.6 * scale, 9.5, 12.5)) * _textScale;
        var widthScale = Math.Min(1.45, _textScale);
        var maxWidth = (isFocus ? 230 : layout.Depth == 1 ? 155 : 125) * widthScale;
        var labelClock = Stopwatch.StartNew();
        var key = new LabelTextKey(
            node.Name,
            CultureInfo.CurrentCulture.Name,
            fontSize,
            maxWidth,
            isFocus ? FontWeight.SemiBold : FontWeight.Normal,
            isFocus ? palette.Text : color);
        var text = _textCache.GetOrAdd(key, static item => new FormattedText(
            item.Text,
            CultureInfo.GetCultureInfo(item.CultureName),
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, item.Weight),
            item.FontSize,
            new SolidColorBrush(item.Color))
        {
            MaxTextWidth = item.MaxWidth,
            TextAlignment = TextAlignment.Center,
            Trimming = TextTrimming.CharacterEllipsis,
        });
        var origin = new Point(center.X - (maxWidth / 2), center.Y + halfHeight + 7);
        var bounds = new LabelBox(origin.X, origin.Y, maxWidth, Math.Max(fontSize + 5, text.Height));
        var prepared = new PreparedLabel(
            new LabelCandidate(node.Id, bounds, presentation.LabelPriority, presentation.LabelIsRequired),
            text,
            origin,
            opacity);
        labelClock.Stop();
        _labelPreparationDuration += labelClock.Elapsed;
        return prepared;
    }

    private static void DrawFolder(
        DrawingContext context,
        Point center,
        double halfWidth,
        double halfHeight,
        Pen stroke)
    {
        var body = new Rect(center.X - halfWidth, center.Y - halfHeight + 4, halfWidth * 2, (halfHeight * 2) - 4);
        var tab = new Rect(center.X - halfWidth + 3, center.Y - halfHeight - 2, halfWidth * 0.78, 8);
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
        context.DrawLine(stroke, new Point(center.X - (halfWidth * 0.55), center.Y), new Point(center.X + (halfWidth * 0.55), center.Y));
    }

    private static void DrawAggregate(
        DrawingContext context,
        Point center,
        double radius,
        Pen stroke,
        IBrush markerBrush,
        bool isNavigable)
    {
        context.DrawEllipse(null, stroke, center, radius, radius);
        context.DrawEllipse(markerBrush, null, center, 2.5, 2.5);
        if (isNavigable)
        {
            context.DrawLine(stroke, new Point(center.X - 5, center.Y), new Point(center.X + 5, center.Y));
            context.DrawLine(stroke, new Point(center.X + 2, center.Y - 3), new Point(center.X + 5, center.Y));
            context.DrawLine(stroke, new Point(center.X + 2, center.Y + 3), new Point(center.X + 5, center.Y));
        }
    }

    private IReadOnlyDictionary<string, GraphLayoutNode> CurrentLayout()
    {
        if (!_animationClock.IsRunning || _animationClock.Elapsed.TotalMilliseconds >= AnimationDurationMilliseconds)
        {
            return _targetLayout;
        }

        var raw = Math.Clamp(_animationClock.Elapsed.TotalMilliseconds / AnimationDurationMilliseconds, 0, 1);
        var amount = 1 - Math.Pow(1 - raw, 3);
        return _targetLayout.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                var from = _animationFrom.TryGetValue(pair.Key, out var source) ? source : pair.Value;
                var target = pair.Value;
                return new GraphLayoutNode(
                    pair.Key,
                    Lerp(from.X, target.X, amount),
                    Lerp(from.Y, target.Y, amount),
                    Lerp(from.Scale, target.Scale, amount),
                    Lerp(from.Opacity, target.Opacity, amount),
                    target.Depth);
            },
            ExplorerIdentity.Comparer);
    }

    private Point ToCanvas(GraphLayoutNode node)
    {
        var width = Math.Max(1, Bounds.Width * 0.66) * _zoom;
        var height = Math.Max(1, Bounds.Height * 0.68) * _zoom;
        return new Point((Bounds.Width / 2) + _pan.X + (node.X * width), (Bounds.Height / 2) + _pan.Y + (node.Y * height));
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

    private void UpdateAutomationDescription()
    {
        var selected = _neighborhood?.Nodes.FirstOrDefault(node =>
            ExplorerIdentity.Equals(node.Id, _selectedNodeId));
        var description = selected is null
            ? "Spatial folder graph. Use arrows to select nodes and Enter to activate."
            : $"Selected {selected.Kind}: {selected.Name}. Use Enter to activate.";
        SetValue(AutomationProperties.HelpTextProperty, description);
    }

    private void NotifyAutomationSceneChanged() =>
        (ControlAutomationPeer.FromElement(this) as GraphSceneAutomationPeer)?.NotifySceneChanged();

    private void NotifyAutomationSelectionChanged() =>
        (ControlAutomationPeer.FromElement(this) as GraphSceneAutomationPeer)?.NotifySelectionChanged();

    private void EnsureCacheContext(ScenePalette palette)
    {
        var renderScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (ReferenceEquals(_cachedPalette, palette) && Math.Abs(_cachedRenderScaling - renderScaling) < 0.001)
        {
            return;
        }

        _cachedPalette = palette;
        _cachedRenderScaling = renderScaling;
        _textCache.Clear();
        _brushCache.Clear();
        _penCache.Clear();
    }

    private SolidColorBrush Brush(Color color, byte alpha = byte.MaxValue)
    {
        var resolved = alpha == byte.MaxValue ? color : WithAlpha(color, alpha);
        return _brushCache.GetOrAdd(resolved, static item => new SolidColorBrush(item));
    }

    private Pen Pen(Color color, byte alpha, double thickness)
    {
        var resolved = alpha == byte.MaxValue ? color : WithAlpha(color, alpha);
        return _penCache.GetOrAdd(
            new PenKey(resolved, thickness),
            item => new Pen(Brush(item.Color), item.Thickness));
    }

    private static double Lerp(double from, double to, double amount) => from + ((to - from) * amount);

    private static byte ToByte(double value) => (byte)Math.Clamp(value, 0, 255);

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private sealed record PreparedLabel(LabelCandidate Candidate, FormattedText Text, Point Origin, double Opacity);

    private readonly record struct LabelTextKey(
        string Text,
        string CultureName,
        double FontSize,
        double MaxWidth,
        FontWeight Weight,
        Color Color);

    private readonly record struct PenKey(Color Color, double Thickness);
}
