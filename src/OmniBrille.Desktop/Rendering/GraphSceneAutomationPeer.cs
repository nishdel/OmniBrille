using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using OmniBrille.Core;

namespace OmniBrille.Desktop.Rendering;

internal sealed class GraphSceneAutomationPeer : ControlAutomationPeer, ISelectionProvider
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

    public void NotifyInteractionChanged()
    {
        foreach (var peer in _nodePeers.Values)
        {
            peer.NotifyInteractionChanged();
        }
    }

    public bool CanSelectMultiple => false;

    public bool IsSelectionRequired => false;

    public IReadOnlyList<AutomationPeer> GetSelection() => _owner.GetAutomationNodes()
        .Where(node => _owner.IsAutomationNodeSelected(node.Id))
        .Select(node => (AutomationPeer)GetOrCreateNodePeer(node.Id))
        .ToArray();

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

        peer = new GraphNodeAutomationPeer(_owner, this, nodeId);
        _nodePeers.Add(nodeId, peer);
        return peer;
    }
}

internal sealed class GraphNodeAutomationPeer : ControlAutomationPeer, IInvokeProvider, ISelectionItemProvider
{
    private readonly GraphSceneControl _owner;
    private readonly GraphSceneAutomationPeer _container;
    private readonly string _nodeId;
    private bool _lastSelected;
    private string? _lastItemStatus;

    public GraphNodeAutomationPeer(GraphSceneControl owner, GraphSceneAutomationPeer container, string nodeId)
        : base(owner)
    {
        _owner = owner;
        _container = container;
        _nodeId = nodeId;
        _lastSelected = owner.IsAutomationNodeSelected(nodeId);
        _lastItemStatus = GetItemStatusCore();
    }

    public void Invoke() => _owner.ActivateAutomationNode(_nodeId);

    public bool IsSelected => _owner.IsAutomationNodeSelected(_nodeId);

    public ISelectionProvider SelectionContainer => _container;

    public void AddToSelection() => Select();

    public void RemoveFromSelection()
    {
        // The graph always retains one selected item.
    }

    public void Select() => _owner.SelectAutomationNode(_nodeId);

    public void NotifyInteractionChanged()
    {
        var selected = _owner.IsAutomationNodeSelected(_nodeId);
        if (selected != _lastSelected)
        {
            var previous = _lastSelected;
            _lastSelected = selected;
            RaisePropertyChangedEvent(
                SelectionItemPatternIdentifiers.IsSelectedProperty,
                previous,
                selected);
        }

        var itemStatus = GetItemStatusCore();
        if (!string.Equals(itemStatus, _lastItemStatus, StringComparison.Ordinal))
        {
            var previousStatus = _lastItemStatus;
            _lastItemStatus = itemStatus;
            RaisePropertyChangedEvent(
                AutomationElementIdentifiers.ItemStatusProperty,
                previousStatus,
                itemStatus);
        }
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

    protected override string? GetAutomationIdCore() => $"GraphNode:{_nodeId}";

    protected override string GetClassNameCore() => "OmniBrilleGraphNode";

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

    protected override string? GetNameCore()
    {
        var node = Node;
        if (node is null)
        {
            return "Unavailable graph node";
        }

        return $"{node.Name}, {DescribeKind(node.Kind)}, {_owner.GetAutomationNodeRelation(_nodeId)}";
    }

    protected override string? GetHelpTextCore()
    {
        var node = Node;
        if (node is null)
        {
            return "This graph node is no longer available.";
        }

        var action = node.IsNavigable ? "Invoke to open." : "Select to inspect details.";
        return string.IsNullOrWhiteSpace(Relationship?.Reason)
            ? $"{node.Path}. {action}"
            : $"{node.Path}. Related because {Relationship.Reason}. {action}";
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

        if (Relationship is not null)
        {
            states.Add("Contextually related");
        }

        if (_owner.IsAutomationNodeHighlighted(_nodeId))
        {
            states.Add("Search match");
        }

        if (Node is not null)
        {
            states.Add(_owner.GetAutomationNodeRelation(_nodeId));
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

    private ExplorerRelationship? Relationship => _owner.GetAutomationRelationship(_nodeId);

    private static string DescribeKind(ExplorerNodeKind kind) => kind switch
    {
        ExplorerNodeKind.Context => "previous folder",
        ExplorerNodeKind.Aggregate => "aggregate",
        ExplorerNodeKind.Folder => "folder",
        ExplorerNodeKind.File => "file",
        _ => "node",
    };

}
