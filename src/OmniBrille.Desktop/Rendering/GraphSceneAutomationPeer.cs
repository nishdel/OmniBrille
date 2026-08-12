using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using OmniBrille.Core;

namespace OmniBrille.Desktop.Rendering;

internal sealed class GraphSceneAutomationPeer : ControlAutomationPeer
{
    private readonly GraphSceneControl _owner;
    private readonly Dictionary<string, GraphNodeAutomationPeer> _nodePeers = new(ExplorerIdentity.Comparer);

    public GraphSceneAutomationPeer(GraphSceneControl owner)
        : base(owner)
    {
        _owner = owner;
    }

    public void NotifySceneChanged()
    {
        var currentIds = _owner.GetAutomationNodes().Select(node => node.Id).ToHashSet(ExplorerIdentity.Comparer);
        foreach (var staleId in _nodePeers.Keys.Where(id => !currentIds.Contains(id)).ToArray())
        {
            _nodePeers.Remove(staleId);
        }

        InvalidateChildren();
    }

    public void NotifySelectionChanged()
    {
        foreach (var peer in _nodePeers.Values)
        {
            peer.NotifySelectionChanged();
        }
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tree;

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => _owner
        .GetAutomationNodes()
        .Select(node => GetOrCreateNodePeer(node.Id))
        .ToArray();

    private GraphNodeAutomationPeer GetOrCreateNodePeer(string nodeId)
    {
        if (_nodePeers.TryGetValue(nodeId, out var peer))
        {
            return peer;
        }

        peer = new GraphNodeAutomationPeer(_owner, nodeId);
        _nodePeers.Add(nodeId, peer);
        return peer;
    }
}

internal sealed class GraphNodeAutomationPeer : ControlAutomationPeer, IInvokeProvider
{
    private readonly GraphSceneControl _owner;
    private readonly string _nodeId;
    private bool _lastSelected;

    public GraphNodeAutomationPeer(GraphSceneControl owner, string nodeId)
        : base(owner)
    {
        _owner = owner;
        _nodeId = nodeId;
        _lastSelected = owner.IsAutomationNodeSelected(nodeId);
    }

    public void Invoke() => _owner.ActivateAutomationNode(_nodeId);

    public void NotifySelectionChanged()
    {
        var selected = _owner.IsAutomationNodeSelected(_nodeId);
        if (selected == _lastSelected)
        {
            return;
        }

        var previous = _lastSelected;
        _lastSelected = selected;
        RaisePropertyChangedEvent(
            AutomationElementIdentifiers.ItemStatusProperty,
            previous ? "Selected" : "Not selected",
            selected ? "Selected" : "Not selected");
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

    protected override string? GetAutomationIdCore() => $"GraphNode:{_nodeId}";

    protected override string GetClassNameCore() => "OmniBrilleGraphNode";

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

    protected override string? GetNameCore()
    {
        var node = Node;
        return node is null ? "Unavailable graph node" : $"{node.Name}, {DescribeKind(node.Kind)}";
    }

    protected override string? GetHelpTextCore()
    {
        var node = Node;
        if (node is null)
        {
            return "This graph node is no longer available.";
        }

        var action = node.IsNavigable ? "Invoke to open." : "Select to inspect details.";
        return $"{node.Path}. {action}";
    }

    protected override string? GetItemTypeCore() => Node?.Kind.ToString();

    protected override string? GetItemStatusCore()
    {
        var states = new List<string>(3);
        if (_owner.IsAutomationNodeSelected(_nodeId))
        {
            states.Add("Selected");
        }

        if (_owner.IsAutomationNodeFocused(_nodeId))
        {
            states.Add("Current focus");
        }

        if (Node?.Kind == ExplorerNodeKind.Aggregate)
        {
            states.Add("Aggregate");
        }

        return states.Count == 0 ? "Visible" : string.Join(", ", states);
    }

    protected override Rect GetBoundingRectangleCore()
    {
        var ownerBounds = base.GetBoundingRectangleCore();
        var nodeBounds = _owner.GetAutomationNodeBounds(_nodeId);
        return nodeBounds == default
            ? ownerBounds
            : new Rect(
                ownerBounds.X + nodeBounds.X,
                ownerBounds.Y + nodeBounds.Y,
                nodeBounds.Width,
                nodeBounds.Height);
    }

    protected override bool HasKeyboardFocusCore() =>
        _owner.IsFocused && _owner.IsAutomationNodeSelected(_nodeId);

    protected override bool IsKeyboardFocusableCore() => true;

    protected override void SetFocusCore() => _owner.SelectAutomationNode(_nodeId);

    private ExplorerNode? Node => _owner.GetAutomationNode(_nodeId);

    private static string DescribeKind(ExplorerNodeKind kind) => kind switch
    {
        ExplorerNodeKind.Context => "previous folder",
        ExplorerNodeKind.Aggregate => "aggregate",
        ExplorerNodeKind.Folder => "folder",
        ExplorerNodeKind.File => "file",
        _ => "node",
    };
}
