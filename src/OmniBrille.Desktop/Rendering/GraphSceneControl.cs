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
    bool MotionActive,
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
    public const double MinimumInteractiveOpacity = 0.85;

    private readonly RadialGraphLayout _structureLayoutEngine = new();
    private readonly ContextGraphLayout _contextLayoutEngine = new();
    private readonly HybridGraphLayout _hybridLayoutEngine = new();
    private readonly DispatcherTimer _animationTimer;
    private readonly Stopwatch _animationClock = new();
    private readonly Stopwatch _motionClock = Stopwatch.StartNew();
    private readonly Dictionary<string, Rect> _hitTargets = new(ExplorerIdentity.Comparer);
    private readonly Dictionary<string, GraphLayoutNode> _renderLayout = new(ExplorerIdentity.Comparer);
    private readonly Dictionary<string, GraphNodePresentation> _presentations = new(ExplorerIdentity.Comparer);
    private readonly Dictionary<string, double> _lensInfluences = new(ExplorerIdentity.Comparer);
    private readonly List<PreparedLabel> _preparedLabels = new(GraphNeighborhoodBuilder.DefaultNodeBudget);
    private readonly List<LabelCandidate> _acceptedLabelCandidates = new(GraphNeighborhoodBuilder.DefaultNodeBudget);
    private readonly HashSet<string> _visibleLabelIds = new(ExplorerIdentity.Comparer);
    private readonly List<ExplorerNode> _drawOrder = new(GraphNeighborhoodBuilder.DefaultNodeBudget);
    private readonly Point[] _backgroundPoints = new Point[42];
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
    private bool _reducedMotion;
    private bool _motionAllowed = true;
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
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(42) };
        _animationTimer.Tick += (_, _) =>
        {
            if (_animationClock.Elapsed.TotalMilliseconds >= AnimationDurationMilliseconds)
            {
                _animationClock.Stop();
            }

            if (!ShouldTick)
            {
                _animationTimer.Stop();
            }

            InvalidateVisual();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _motionAllowed = false;
            UpdateTicker();
        };
        AttachedToVisualTree += (_, _) =>
        {
            _motionAllowed = true;
            UpdateTicker();
        };
        GotFocus += (_, _) => NotifyAutomationInteractionChanged();
        LostFocus += (_, _) => NotifyAutomationInteractionChanged();
    }

    public event EventHandler<string>? NodeSelected;

    public event EventHandler<string>? NodeActivated;

    public event EventHandler<string?>? NodeHovered;

    public event EventHandler? BackRequested;

    public event EventHandler? DismissRequested;

    public bool ReducedMotion
    {
        get => _reducedMotion;
        set
        {
            if (_reducedMotion == value)
            {
                return;
            }

            _reducedMotion = value;
            if (value)
            {
                _animationClock.Reset();
                _animationFrom.Clear();
            }
            UpdateTicker();
            InvalidateVisual();
        }
    }

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
        ShouldAnimateMotion,
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

    internal bool ShouldAnimateMotion => !_reducedMotion && _motionAllowed && _neighborhood is not null;

    private bool ShouldTick => _animationClock.IsRunning || ShouldAnimateMotion;

    public void SetMotionActivity(bool active)
    {
        _motionAllowed = active;
        if (!active)
        {
            _animationClock.Reset();
            _animationFrom.Clear();
        }
        UpdateTicker();
    }

    public void SetScene(
        ExplorerNeighborhood? neighborhood,
        string? selectedNodeId,
        IReadOnlySet<string>? highlights,
        bool animate = true)
    {
        var preparationClock = Stopwatch.StartNew();
        var previousLayout = CurrentLayout();
        _neighborhood = neighborhood;
        if (_hoveredNodeId is not null &&
            (neighborhood is null || !neighborhood.Nodes.Any(node => ExplorerIdentity.Equals(node.Id, _hoveredNodeId))))
        {
            _hoveredNodeId = null;
        }

        UpdateLensInfluences();
        var selectionChanged = !ExplorerIdentity.Equals(_selectedNodeId, selectedNodeId);
        _selectedNodeId = selectedNodeId;
        _highlights = highlights ?? new HashSet<string>();
        _drawOrder.Clear();
        if (neighborhood is not null)
        {
            _drawOrder.AddRange(neighborhood.Nodes
                .OrderBy(node => node.Id == neighborhood.FocusNodeId ? 1 : 0)
                .ThenBy(node => node.Id, ExplorerIdentity.Comparer));
        }

        var layoutClock = Stopwatch.StartNew();
        _targetLayout = neighborhood?.ViewMode switch
        {
            ExplorerViewMode.Context => _contextLayoutEngine.Layout(neighborhood, previousLayout),
            ExplorerViewMode.Hybrid => _hybridLayoutEngine.Layout(neighborhood, previousLayout),
            null => new Dictionary<string, GraphLayoutNode>(),
            _ => _structureLayoutEngine.Layout(neighborhood, previousLayout),
        };
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
            UpdateTicker();
        }
        else
        {
            _animationClock.Reset();
            UpdateTicker();
        }

        UpdateAutomationDescription();
        NotifyAutomationSceneChanged();
        if (selectionChanged)
        {
            NotifyAutomationInteractionChanged();
        }
        preparationClock.Stop();
        _scenePreparationDuration = preparationClock.Elapsed;
        InvalidateVisual();
    }

    public void SetInteractionState(
        string? selectedNodeId,
        IReadOnlySet<string>? highlights,
        bool searchActive)
    {
        var selectionChanged = !ExplorerIdentity.Equals(_selectedNodeId, selectedNodeId);
        _selectedNodeId = selectedNodeId;
        _highlights = highlights ?? new HashSet<string>();
        SearchActive = searchActive;
        UpdateAutomationDescription();
        NotifyAutomationInteractionChanged();

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
        _presentations.Clear();
        foreach (var node in _neighborhood.Nodes)
        {
            _presentations[node.Id] = GraphPresentationPolicy.Evaluate(node, layout[node.Id], presentationContext);
        }

        _hitTargets.Clear();
        phaseClock.Restart();
        DrawEdges(context, palette, layout, _presentations);
        phaseClock.Stop();
        _edgeDuration = phaseClock.Elapsed;

        _preparedLabels.Clear();
        phaseClock.Restart();
        foreach (var node in _drawOrder)
        {
            if (!layout.TryGetValue(node.Id, out var position))
            {
                continue;
            }

            var preparedLabel = DrawNodeGlyph(context, palette, node, position, _presentations[node.Id]);
            if (preparedLabel.HasValue)
            {
                _preparedLabels.Add(preparedLabel.Value);
            }
        }
        phaseClock.Stop();
        _glyphDuration = phaseClock.Elapsed - _labelPreparationDuration;

        var labelBudget = GraphPresentationPolicy.RecommendedLabelBudget(_zoom, _neighborhood.Nodes.Count, _textScale);
        phaseClock.Restart();
        ResolvePreparedLabels(labelBudget, ReducedEffects ? 3 : 5);
        phaseClock.Stop();
        _labelCollisionDuration = phaseClock.Elapsed;
        phaseClock.Restart();
        foreach (var label in _preparedLabels)
        {
            if (!_visibleLabelIds.Contains(label.Candidate.NodeId))
            {
                continue;
            }

            using (context.PushOpacity(label.Opacity))
            {
                context.DrawText(label.Text, label.Origin);
            }
        }
        phaseClock.Stop();
        _labelDrawDuration = phaseClock.Elapsed;

        _renderedLabelCount = _visibleLabelIds.Count;
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
            NotifyAutomationInteractionChanged();
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
            UpdateLensInfluences();
            NodeHovered?.Invoke(this, hovered);
            UpdateTicker();
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredNodeId is not null)
        {
            _hoveredNodeId = null;
            UpdateLensInfluences();
            NodeHovered?.Invoke(this, null);
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
            var selected = FindDirectionalNode(e.Key);
            if (selected is null)
            {
                return;
            }

            _selectedNodeId = selected.Id;
            NodeSelected?.Invoke(this, _selectedNodeId);
            UpdateAutomationDescription();
            NotifyAutomationInteractionChanged();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    internal IReadOnlyList<ExplorerNode> GetAutomationNodes() => _neighborhood?.Nodes ?? [];

    internal ExplorerNode? GetAutomationNode(string nodeId) => _neighborhood?.Nodes.FirstOrDefault(node =>
        ExplorerIdentity.Equals(node.Id, nodeId));

    internal ExplorerRelationship? GetAutomationRelationship(string nodeId) => _neighborhood?.Edges
        .Where(edge => edge.Kind == ExplorerGraphEdgeKind.Contextual && edge.Relationship is not null)
        .Where(edge =>
            (ExplorerIdentity.Equals(edge.SourceId, _neighborhood.FocusNodeId) && ExplorerIdentity.Equals(edge.TargetId, nodeId)) ||
            (ExplorerIdentity.Equals(edge.TargetId, _neighborhood.FocusNodeId) && ExplorerIdentity.Equals(edge.SourceId, nodeId)))
        .OrderByDescending(edge => edge.Relationship!.Strength)
        .Select(edge => edge.Relationship)
        .FirstOrDefault();

    internal bool IsAutomationNodeSelected(string nodeId) =>
        ExplorerIdentity.Equals(nodeId, _selectedNodeId);

    internal bool IsAutomationNodeFocused(string nodeId) =>
        ExplorerIdentity.Equals(nodeId, _neighborhood?.FocusNodeId);

    internal bool IsAutomationNodeHighlighted(string nodeId) => _highlights.Contains(nodeId);

    internal string GetAutomationNodeRelation(string nodeId)
    {
        var node = GetAutomationNode(nodeId);
        if (node is null || _neighborhood is null)
        {
            return "visible graph item";
        }

        var relation = ExplorerSceneSemantics.Describe(_neighborhood, node);
        if (_neighborhood.ViewMode != ExplorerViewMode.Hybrid)
        {
            return relation;
        }

        var structural = HasRole(node, ExplorerNodeRole.Structural);
        var contextual = HasRole(node, ExplorerNodeRole.Contextual);
        var roles = (structural, contextual) switch
        {
            (true, true) => "structural and contextually related",
            (true, false) => "structural",
            (false, true) => "contextually related",
            _ => "visible",
        };
        return $"{relation}, {roles}";
    }

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
        NotifyAutomationInteractionChanged();
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
        for (var index = 0; index < density; index++)
        {
            var x = ((index * 127) % 997) / 997d * Bounds.Width;
            var y = ((index * 283 + 71) % 991) / 991d * Bounds.Height;
            _backgroundPoints[index] = new Point(x, y);
            context.DrawEllipse(pointBrush, null, _backgroundPoints[index], index % 7 == 0 ? 1.8 : 1.05, index % 7 == 0 ? 1.8 : 1.05);
            if (index > 2 && index % 2 == 0)
            {
                context.DrawLine(linePen, _backgroundPoints[index - 2], _backgroundPoints[index]);
            }

            if (!ReducedEffects && index > 4 && index % 5 == 0)
            {
                context.DrawLine(trianglePen, _backgroundPoints[index - 5], _backgroundPoints[index - 2]);
                context.DrawLine(trianglePen, _backgroundPoints[index - 2], _backgroundPoints[index]);
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
            var sourcePresentation = presentations[edge.SourceId];
            var targetPresentation = presentations[edge.TargetId];
            var opacity = Math.Min(source.Opacity, target.Opacity) *
                Math.Min(sourcePresentation.OpacityMultiplier, targetPresentation.OpacityMultiplier);
            var edgeBand = Math.Max(source.PresentationBand, target.PresentationBand);
            var selected = _selectedNodeId is not null &&
                !ExplorerIdentity.Equals(_selectedNodeId, _neighborhood.FocusNodeId) &&
                (ExplorerIdentity.Equals(edge.SourceId, _selectedNodeId) ||
                 ExplorerIdentity.Equals(edge.TargetId, _selectedNodeId));
            var hovered = _hoveredNodeId is not null &&
                !ExplorerIdentity.Equals(_hoveredNodeId, _neighborhood.FocusNodeId) &&
                (ExplorerIdentity.Equals(edge.SourceId, _hoveredNodeId) ||
                 ExplorerIdentity.Equals(edge.TargetId, _hoveredNodeId));
            var highlighted =
                (!ExplorerIdentity.Equals(edge.SourceId, _neighborhood.FocusNodeId) &&
                 _highlights.Contains(edge.SourceId)) ||
                (!ExplorerIdentity.Equals(edge.TargetId, _neighborhood.FocusNodeId) &&
                 _highlights.Contains(edge.TargetId));
            var emphasized = selected || hovered || highlighted;
            if (emphasized)
            {
                opacity = Math.Max(opacity, 0.64);
            }

            if (edge.Kind == ExplorerGraphEdgeKind.Contextual)
            {
                var strength = Math.Clamp((edge.Relationship?.Strength ?? 50) / 100d, 0.35, 1);
                if (!ReducedEffects && emphasized)
                {
                    context.DrawLine(
                        Pen(palette.ContextEdgeGlow, ToByte(58 * opacity), 5),
                        start,
                        end);
                }

                DrawDashedLine(
                    context,
                    Pen(
                        palette.ContextEdge,
                        ToByte((emphasized ? 235 : 155) * opacity * strength),
                        emphasized ? 1.65 : 0.95),
                    start,
                    end,
                    ReducedEffects ? 7 : 6,
                    ReducedEffects ? 6 : 4);
                context.DrawEllipse(
                    Brush(palette.ContextEdgeGlow, ToByte((emphasized ? 235 : 170) * opacity)),
                    null,
                    end,
                    emphasized ? 2.4 : 1.55,
                    emphasized ? 2.4 : 1.55);
                continue;
            }

            if (!ReducedEffects && (edgeBand <= 1 || emphasized))
            {
                context.DrawLine(
                    Pen(
                        emphasized ? palette.Selection : palette.EdgeGlow,
                        ToByte((emphasized ? 52 : 26) * opacity * Math.Max(sourcePresentation.GlowMultiplier, targetPresentation.GlowMultiplier)),
                        emphasized ? 5.5 : 4.2),
                    start,
                    end);
            }

            var edgeMultiplier = emphasized
                ? 1
                : Math.Min(sourcePresentation.EdgeMultiplier, targetPresentation.EdgeMultiplier);
            context.DrawLine(
                Pen(
                    emphasized ? palette.Selection : palette.Edge,
                    ToByte((emphasized ? 225 : 182) * opacity * edgeMultiplier),
                    emphasized ? 1.6 : edgeBand <= 1 ? 1.2 : edgeBand == 2 ? 0.76 : 0.52),
                start,
                end);
            context.DrawEllipse(
                Brush(palette.EdgeGlow, ToByte(220 * opacity * edgeMultiplier)),
                null,
                end,
                emphasized || edgeBand <= 1 ? 2.1 : 1.2,
                emphasized || edgeBand <= 1 ? 2.1 : 1.2);
        }
    }

    private static void DrawDashedLine(
        DrawingContext context,
        Pen pen,
        Point start,
        Point end,
        double dashLength,
        double gapLength)
    {
        var vector = new Vector(end.X - start.X, end.Y - start.Y);
        var length = vector.Length;
        if (length <= 0.001)
        {
            return;
        }

        var direction = vector / length;
        for (var offset = 0d; offset < length; offset += dashLength + gapLength)
        {
            var segmentEnd = Math.Min(length, offset + dashLength);
            context.DrawLine(pen, start + (direction * offset), start + (direction * segmentEnd));
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
        var opacity = Math.Max(
            MinimumInteractiveOpacity,
            Math.Clamp(layout.Opacity * presentation.OpacityMultiplier, 0, 1));
        var color = isHighlighted
            ? palette.Search
            : isFocus
                ? palette.Focus
                : _neighborhood.ViewMode == ExplorerViewMode.Context ||
                  (_neighborhood.ViewMode == ExplorerViewMode.Hybrid && HasRole(node, ExplorerNodeRole.Contextual) && !HasRole(node, ExplorerNodeRole.Structural))
                    ? palette.ContextEdge
                : node.Kind == ExplorerNodeKind.Context
                    ? palette.Context
                    : palette.Node;

        if (presentation.LevelOfDetail == GraphLevelOfDetail.Point)
        {
            context.DrawEllipse(Brush(color, ToByte(220 * opacity)), null, center, 2.2, 2.2);
            _hitTargets[node.Id] = new Rect(center.X - 22, center.Y - 22, 44, 44);
            return null;
        }

        var stroke = Pen(
            color,
            ToByte(250 * opacity),
            isFocus ? 2.05 : layout.Depth == 1 ? 1.25 : 0.95);
        var halfWidth = 25 * scale;
        var halfHeight = 19 * scale;
        var hitWidth = Math.Max(44, (halfWidth + 10) * 2);
        var hitHeight = Math.Max(44, (halfHeight + 26) * 2);
        _hitTargets[node.Id] = new Rect(
            center.X - (hitWidth / 2),
            center.Y - (hitHeight / 2),
            hitWidth,
            hitHeight);

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

        if (isFocus)
        {
            DrawFocusReticle(
                context,
                center,
                halfWidth + 17,
                halfHeight + 15,
                Pen(color, ToByte((ReducedEffects ? 185 : 235) * opacity), 1.15));
        }

        if (IsFocused && isSelected)
        {
            DrawFocusReticle(
                context,
                center,
                halfWidth + 22,
                halfHeight + 20,
                Pen(palette.Selection, 255, 2.15));
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

        if (_neighborhood.ViewMode == ExplorerViewMode.Hybrid &&
            HasRole(node, ExplorerNodeRole.Structural) &&
            HasRole(node, ExplorerNodeRole.Contextual))
        {
            var marker = new Point(center.X + halfWidth - 2, center.Y - halfHeight + 1);
            context.DrawEllipse(
                Brush(palette.ContextEdgeGlow, ToByte(235 * opacity)),
                Pen(palette.Background, ToByte(210 * opacity), 1),
                marker,
                3.1,
                3.1);
        }

        if (presentation.LevelOfDetail < GraphLevelOfDetail.Labeled)
        {
            return null;
        }

        var fontSize = (isFocus ? 14.5 : Math.Clamp(11.6 * scale, 9.5, 12.5)) * _textScale;
        var widthScale = Math.Min(1.45, _textScale);
        var maxWidth = (isFocus ? 230 : layout.PresentationBand <= 1 ? 155 : 135) * widthScale;
        var labelClock = Stopwatch.StartNew();
        var key = new LabelTextKey(
            node.Name,
            CultureInfo.CurrentCulture.Name,
            fontSize,
            maxWidth,
            isFocus ? FontWeight.SemiBold : FontWeight.Normal,
            palette.Text);
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
            1);
        labelClock.Stop();
        _labelPreparationDuration += labelClock.Elapsed;
        return prepared;
    }

    private void ResolvePreparedLabels(int maximumLabels, double padding)
    {
        _preparedLabels.Sort(static (left, right) =>
        {
            var required = right.Candidate.IsRequired.CompareTo(left.Candidate.IsRequired);
            if (required != 0)
            {
                return required;
            }

            var priority = right.Candidate.Priority.CompareTo(left.Candidate.Priority);
            return priority != 0
                ? priority
                : ExplorerIdentity.Comparer.Compare(left.Candidate.NodeId, right.Candidate.NodeId);
        });
        _acceptedLabelCandidates.Clear();
        _visibleLabelIds.Clear();
        foreach (var prepared in _preparedLabels)
        {
            var candidate = prepared.Candidate;
            if (!candidate.IsRequired && _acceptedLabelCandidates.Count >= maximumLabels)
            {
                continue;
            }

            var overlaps = false;
            if (!candidate.IsRequired)
            {
                foreach (var accepted in _acceptedLabelCandidates)
                {
                    if (candidate.Bounds.Intersects(accepted.Bounds, padding))
                    {
                        overlaps = true;
                        break;
                    }
                }
            }

            if (overlaps)
            {
                continue;
            }

            _acceptedLabelCandidates.Add(candidate);
            _visibleLabelIds.Add(candidate.NodeId);
        }
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

    private static void DrawFocusReticle(
        DrawingContext context,
        Point center,
        double halfWidth,
        double halfHeight,
        Pen pen)
    {
        const double arm = 8;
        context.DrawLine(pen, new Point(center.X - halfWidth, center.Y - halfHeight), new Point(center.X - halfWidth + arm, center.Y - halfHeight));
        context.DrawLine(pen, new Point(center.X - halfWidth, center.Y - halfHeight), new Point(center.X - halfWidth, center.Y - halfHeight + arm));
        context.DrawLine(pen, new Point(center.X + halfWidth, center.Y - halfHeight), new Point(center.X + halfWidth - arm, center.Y - halfHeight));
        context.DrawLine(pen, new Point(center.X + halfWidth, center.Y - halfHeight), new Point(center.X + halfWidth, center.Y - halfHeight + arm));
        context.DrawLine(pen, new Point(center.X - halfWidth, center.Y + halfHeight), new Point(center.X - halfWidth + arm, center.Y + halfHeight));
        context.DrawLine(pen, new Point(center.X - halfWidth, center.Y + halfHeight), new Point(center.X - halfWidth, center.Y + halfHeight - arm));
        context.DrawLine(pen, new Point(center.X + halfWidth, center.Y + halfHeight), new Point(center.X + halfWidth - arm, center.Y + halfHeight));
        context.DrawLine(pen, new Point(center.X + halfWidth, center.Y + halfHeight), new Point(center.X + halfWidth, center.Y + halfHeight - arm));
    }

    private IReadOnlyDictionary<string, GraphLayoutNode> CurrentLayout()
    {
        if ((!_animationClock.IsRunning || _animationClock.Elapsed.TotalMilliseconds >= AnimationDurationMilliseconds) &&
            !ShouldAnimateMotion)
        {
            return _targetLayout;
        }

        var amount = 1d;
        if (_animationClock.IsRunning)
        {
            var raw = Math.Clamp(_animationClock.Elapsed.TotalMilliseconds / AnimationDurationMilliseconds, 0, 1);
            amount = 1 - Math.Pow(1 - raw, 3);
        }

        _renderLayout.Clear();
        foreach (var pair in _targetLayout)
        {
            var target = pair.Value;
            var from = _animationFrom.TryGetValue(pair.Key, out var source) ? source : target;
            var interpolated = new GraphLayoutNode(
                pair.Key,
                Lerp(from.X, target.X, amount),
                Lerp(from.Y, target.Y, amount),
                Lerp(from.Scale, target.Scale, amount),
                Lerp(from.Opacity, target.Opacity, amount),
                target.Depth,
                target.PresentationBand);
            var influence = LensInfluence(pair.Key);
            _renderLayout[pair.Key] = GraphMotionPolicy.Evaluate(
                interpolated,
                _motionClock.Elapsed.TotalSeconds,
                influence,
                !ShouldAnimateMotion,
                ExplorerIdentity.Equals(pair.Key, _neighborhood?.FocusNodeId));
        }

        return _renderLayout;
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

    private double LensInfluence(string nodeId) =>
        _lensInfluences.TryGetValue(nodeId, out var influence) ? influence : 0;

    private void UpdateLensInfluences()
    {
        _lensInfluences.Clear();
        if (_hoveredNodeId is null || _neighborhood is null)
        {
            return;
        }

        _lensInfluences[_hoveredNodeId] = 1;
        foreach (var edge in _neighborhood.Edges)
        {
            if (ExplorerIdentity.Equals(edge.SourceId, _hoveredNodeId))
            {
                _lensInfluences[edge.TargetId] = 0.42;
            }
            else if (ExplorerIdentity.Equals(edge.TargetId, _hoveredNodeId))
            {
                _lensInfluences[edge.SourceId] = 0.42;
            }
        }

        var oneHop = _lensInfluences
            .Where(pair => pair.Value == 0.42)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var intermediate in oneHop)
        {
            foreach (var edge in _neighborhood.Edges)
            {
                string? neighbor = null;
                if (ExplorerIdentity.Equals(edge.SourceId, intermediate))
                {
                    neighbor = edge.TargetId;
                }
                else if (ExplorerIdentity.Equals(edge.TargetId, intermediate))
                {
                    neighbor = edge.SourceId;
                }

                if (neighbor is not null && !_lensInfluences.ContainsKey(neighbor))
                {
                    _lensInfluences[neighbor] = 0.16;
                }
            }
        }
    }

    private ExplorerNode? FindDirectionalNode(Key key)
    {
        if (_neighborhood is null || !_targetLayout.TryGetValue(_selectedNodeId ?? _neighborhood.FocusNodeId, out var origin))
        {
            return _neighborhood?.Focus;
        }

        var desired = key switch
        {
            Key.Left => new Vector(-1, 0),
            Key.Right => new Vector(1, 0),
            Key.Up => new Vector(0, -1),
            _ => new Vector(0, 1),
        };
        var candidates = _neighborhood.Nodes
            .Where(node => !ExplorerIdentity.Equals(node.Id, origin.NodeId) && _targetLayout.ContainsKey(node.Id))
            .Select(node =>
            {
                var candidate = _targetLayout[node.Id];
                var delta = new Vector(candidate.X - origin.X, candidate.Y - origin.Y);
                var length = Math.Max(0.0001, delta.Length);
                var alignment = ((delta.X * desired.X) + (delta.Y * desired.Y)) / length;
                return new { Node = node, Alignment = alignment, Distance = length };
            })
            .ToArray();
        return candidates
            .Where(item => item.Alignment > 0.15)
            .OrderByDescending(item => item.Alignment)
            .ThenBy(item => item.Distance)
            .ThenBy(item => item.Node.Id, ExplorerIdentity.Comparer)
            .Select(item => item.Node)
            .FirstOrDefault() ?? candidates
                .OrderBy(item => item.Distance)
                .ThenBy(item => item.Node.Id, ExplorerIdentity.Comparer)
                .Select(item => item.Node)
                .FirstOrDefault();
    }

    private void UpdateTicker()
    {
        if (ShouldTick)
        {
            _animationTimer.Start();
        }
        else
        {
            _animationTimer.Stop();
        }
    }

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
            ? _neighborhood?.ViewMode switch
            {
                ExplorerViewMode.Context => "Spatial Context graph. Use arrows to select related nodes and Enter to refocus.",
                ExplorerViewMode.Hybrid => "Spatial Hybrid graph. Structure forms the navigation skeleton and Context shows authoritative relationships. Use arrows to select nodes and Enter to refocus.",
                _ => "Spatial folder graph. Use arrows to select nodes and Enter to activate.",
            }
            : $"Selected {selected.Kind}: {selected.Name}. Use Enter to activate.";
        SetValue(AutomationProperties.HelpTextProperty, description);
    }

    private void NotifyAutomationSceneChanged() =>
        (ControlAutomationPeer.FromElement(this) as GraphSceneAutomationPeer)?.NotifySceneChanged();

    private void NotifyAutomationInteractionChanged() =>
        (ControlAutomationPeer.FromElement(this) as GraphSceneAutomationPeer)?.NotifyInteractionChanged();

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

    private static bool HasRole(ExplorerNode node, ExplorerNodeRole role) => (node.Roles & role) == role;

    private readonly record struct PreparedLabel(LabelCandidate Candidate, FormattedText Text, Point Origin, double Opacity);

    private readonly record struct LabelTextKey(
        string Text,
        string CultureName,
        double FontSize,
        double MaxWidth,
        FontWeight Weight,
        Color Color);

    private readonly record struct PenKey(Color Color, double Thickness);
}
