using System.Diagnostics;
using OmniBrille.Core;

namespace OmniBrille.Desktop.Presentation;

public sealed class ExplorerSession : IDisposable
{
    private const int ProgressiveBatchSize = 32;

    private readonly GraphNeighborhoodBuilder _neighborhoodBuilder;
    private readonly ContextNeighborhoodBuilder _contextBuilder;
    private readonly HybridNeighborhoodBuilder _hybridBuilder;
    private readonly NavigationState _navigation = new();
    private IExplorerProvider? _provider;
    private IExplorerSearchProvider? _searchProvider;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _searchCancellation;
    private CancellationTokenSource? _detailsCancellation;
    private ExplorerDirectorySnapshot? _currentSnapshot;
    private ExplorerContextSnapshot? _currentContextSnapshot;
    private ExplorerDirectorySnapshot? _hybridStructureSnapshot;
    private ExplorerEntry? _previousContext;
    private AggregatePage? _aggregatePage;
    private readonly List<ConnectedHistoryEntry> _connectedHistory = [];
    private string? _structureReturnTarget;
    private string? _structureReturnSelectionId;
    private long _loadRequestVersion;
    private long _searchRequestVersion;
    private long _detailsRequestVersion;
    private long _providerGeneration;

    public ExplorerSession(
        GraphNeighborhoodBuilder? neighborhoodBuilder = null,
        ContextNeighborhoodBuilder? contextBuilder = null,
        HybridNeighborhoodBuilder? hybridBuilder = null)
    {
        _neighborhoodBuilder = neighborhoodBuilder ?? new GraphNeighborhoodBuilder();
        _contextBuilder = contextBuilder ?? new ContextNeighborhoodBuilder();
        _hybridBuilder = hybridBuilder ?? new HybridNeighborhoodBuilder();
    }

    public event EventHandler? StateChanged;

    public event Action<Exception>? ProviderFailed;

    public ExplorerNeighborhood? Neighborhood { get; private set; }

    public ExplorerNode? SelectedNode { get; private set; }

    public ExplorerSearchResult? SearchResult { get; private set; }

    public ExplorerNodeDetails? SelectedNodeDetails { get; private set; }

    public ExplorerRelationship? SelectedRelationship => Neighborhood?.Edges
        .Where(edge => edge.Kind == ExplorerGraphEdgeKind.Contextual && edge.Relationship is not null)
        .Where(edge => SelectedNode is not null &&
            ((ExplorerIdentity.Equals(edge.SourceId, Neighborhood.FocusNodeId) && ExplorerIdentity.Equals(edge.TargetId, SelectedNode.Id)) ||
             (ExplorerIdentity.Equals(edge.TargetId, Neighborhood.FocusNodeId) && ExplorerIdentity.Equals(edge.SourceId, SelectedNode.Id))))
        .OrderByDescending(edge => edge.Relationship!.Strength)
        .ThenBy(edge => edge.Relationship!.Id, ExplorerIdentity.Comparer)
        .Select(edge => edge.Relationship)
        .FirstOrDefault();

    public ContextFilter ContextFilter { get; private set; } = ContextFilter.None;

    public ContextFilterSummary? ContextFilterSummary { get; private set; }

    public IReadOnlySet<string> HighlightedNodeIds { get; private set; } = new HashSet<string>();

    public string CurrentPath => Neighborhood?.Focus.Path ?? _navigation.CurrentPath ?? string.Empty;

    public string AccessRoot => _navigation.AccessRoot ?? string.Empty;

    public string SearchQuery { get; private set; } = string.Empty;

    public string Status { get; private set; } = "Choose a folder to begin. Only that location will be accessible.";

    public ExplorerLoadState LoadState { get; private set; } = ExplorerLoadState.Idle;

    public ExplorerViewMode ViewMode { get; private set; } = ExplorerViewMode.Structure;

    public bool IsContextAvailable => ProviderMode == ExplorerProviderMode.Connected && _provider is IExplorerContextProvider;

    public bool IsLoading => LoadState is ExplorerLoadState.Loading or ExplorerLoadState.PartiallyLoaded;

    public bool IsSearching { get; private set; }

    public int LoadedItemCount { get; private set; }

    public TimeSpan LastLoadDuration { get; private set; }

    public int SceneBudget => _neighborhoodBuilder.NodeBudget;

    public ExplorerProviderMode ProviderMode => _provider?.Mode ?? ExplorerProviderMode.Standalone;

    public string ProviderDisplayName => ProviderMode == ExplorerProviderMode.Connected
        ? "Connected · OmniSorSe"
        : "Standalone";

    public bool CanGoBack => ViewMode != ExplorerViewMode.Structure || _aggregatePage is not null || _navigation.CanGoBack;

    public bool IsAggregateRefined => _aggregatePage is not null;

    /// <summary>
    /// Changes whenever provider-specific identities are replaced or cleared. Voice and other
    /// deferred inputs use this value to reject work that completed for an obsolete authority.
    /// </summary>
    public long ProviderGeneration => Volatile.Read(ref _providerGeneration);

    public ExplorerFailureKind CurrentFailure => _currentSnapshot?.Failure ?? ExplorerFailureKind.None;

    public async Task OpenRootAsync(
        IExplorerProvider provider,
        IExplorerSearchProvider searchProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(searchProvider);

        CancelActiveOperations(reportCancellation: false);
        Interlocked.Increment(ref _providerGeneration);
        _provider = provider;
        _searchProvider = searchProvider;
        _navigation.SetRoot(provider.AccessRoot, provider.Mode);
        SearchResult = null;
        SearchQuery = string.Empty;
        HighlightedNodeIds = new HashSet<string>();
        SelectedNode = null;
        SelectedNodeDetails = null;
        Neighborhood = null;
        _currentSnapshot = null;
        _currentContextSnapshot = null;
        _hybridStructureSnapshot = null;
        _previousContext = null;
        _aggregatePage = null;
        ViewMode = ExplorerViewMode.Structure;
        _connectedHistory.Clear();
        _structureReturnTarget = null;
        _structureReturnSelectionId = null;
        ContextFilter = ContextFilter.None;
        ContextFilterSummary = null;

        await LoadDirectoryAsync(
            provider.AccessRoot,
            previousContext: null,
            preferredSelectionId: null,
            commitNavigation: null,
            showFailureScene: true,
            cancellationToken);
    }

    public async Task<bool> NavigateAsync(
        string path,
        string? preferredSelectionId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureProvider();
        var previousFocus = Neighborhood?.Focus;
        var outcome = await LoadDirectoryAsync(
            path,
            ToEntry(previousFocus),
            preferredSelectionId,
            () => _navigation.NavigateTo(path),
            showFailureScene: false,
            cancellationToken);
        return outcome.Applied && outcome.Failure == ExplorerFailureKind.None;
    }

    public async Task<bool> GoBackAsync(CancellationToken cancellationToken = default)
    {
        EnsureProvider();
        if (ViewMode != ExplorerViewMode.Structure)
        {
            if (_connectedHistory.Count > 0)
            {
                var index = _connectedHistory.Count - 1;
                var previous = _connectedHistory[index];
                var restored = await LoadConnectedSceneAsync(
                    previous.FocusNodeId,
                    previous.ViewMode,
                    pushHistory: false,
                    cancellationToken);
                if (restored)
                {
                    _connectedHistory.RemoveAt(index);
                }

                return restored;
            }

            return await SwitchToStructureAsync(cancellationToken);
        }

        if (_aggregatePage is not null)
        {
            _aggregatePage = null;
            RebuildCurrentScene();
            SelectedNode = Neighborhood?.Focus;
            Status = "Returned to the aggregate overview.";
            NotifyChanged();
            return true;
        }

        var target = _navigation.History.Count == 0
            ? null
            : _navigation.History[^1];
        if (target is null)
        {
            return false;
        }

        var previousFocus = Neighborhood?.Focus;
        var outcome = await LoadDirectoryAsync(
            target,
            ToEntry(previousFocus),
            preferredSelectionId: null,
            () => _navigation.GoBack(),
            showFailureScene: false,
            cancellationToken);
        return outcome.Applied;
    }

    public bool ActivateAggregate(string nodeId)
    {
        if (_currentSnapshot is null || Neighborhood is null)
        {
            return false;
        }

        var node = Neighborhood.Nodes.FirstOrDefault(item =>
            ExplorerIdentity.Equals(item.Id, nodeId));
        var action = node?.AggregateAction;
        if (node?.Kind != ExplorerNodeKind.Aggregate || action is null)
        {
            return false;
        }

        _aggregatePage = action.Kind == AggregateActionKind.Overview
            ? null
            : new AggregatePage(action.TargetOffset ?? 0, 0);
        RebuildCurrentScene();
        SelectedNode = Neighborhood?.Focus;
        UpdateHighlights(notify: false);
        Status = _aggregatePage is null
            ? "Aggregate overview restored."
            : $"Showing a bounded aggregate page from item {_aggregatePage.Offset + 1:N0}. Back returns to the overview.";
        NotifyChanged();
        return true;
    }

    public async Task SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        EnsureProvider();
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestVersion = Interlocked.Increment(ref _searchRequestVersion);
        SearchQuery = query.Trim();

        if (SearchQuery.Length == 0)
        {
            SearchResult = null;
            HighlightedNodeIds = new HashSet<string>();
            IsSearching = false;
            Status = "Search cleared.";
            NotifyChanged();
            return;
        }

        IsSearching = true;
        Status = $"Searching the selected root for ‘{SearchQuery}’…";
        NotifyChanged();
        try
        {
            var result = await _searchProvider!.SearchAsync(
                new SearchRequest(
                    AccessRoot,
                    SearchQuery,
                    IncludeContext: ViewMode != ExplorerViewMode.Structure && ProviderMode == ExplorerProviderMode.Connected),
                _searchCancellation.Token);
            if (requestVersion != Volatile.Read(ref _searchRequestVersion))
            {
                (_provider as IExplorerProviderDiagnostics)?.ReportStaleResponseRejected();
                return;
            }

            SearchResult = result;
            UpdateHighlights(notify: false);
            var suffix = result.WasTruncated ? " · bounded" : string.Empty;
            Status = ProviderMode == ExplorerProviderMode.Connected
                ? $"{result.Hits.Count:N0} indexed matches from OmniSorSe{suffix}."
                : $"{result.Hits.Count:N0} matches across {result.DirectoriesVisited:N0} folders{suffix}.";
            if (!string.IsNullOrWhiteSpace(result.Warning))
            {
                Status += $" {result.Warning}";
            }
        }
        catch (OperationCanceledException) when (requestVersion == Volatile.Read(ref _searchRequestVersion))
        {
            Status = "Search cancelled.";
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            Status = ProviderMode == ExplorerProviderMode.Connected
                ? "OmniSorSe Search is temporarily unavailable. The current graph remains visible."
                : "Search failed safely.";
            ProviderFailed?.Invoke(exception);
        }
        finally
        {
            if (requestVersion == Volatile.Read(ref _searchRequestVersion))
            {
                IsSearching = false;
                NotifyChanged();
            }
        }
    }

    public async Task<bool> FocusSearchHitAsync(
        ExplorerSearchHit hit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hit);

        if (ViewMode != ExplorerViewMode.Structure && ProviderMode == ExplorerProviderMode.Connected)
        {
            return await FocusConnectedNodeAsync(hit.Target, ViewMode, cancellationToken);
        }

        if (hit.Kind == ExplorerNodeKind.Folder)
        {
            return await NavigateAsync(hit.Target, cancellationToken: cancellationToken);
        }

        var parent = ProviderMode == ExplorerProviderMode.Connected
            ? hit.ParentNavigationTarget
            : Path.GetDirectoryName(hit.Path);
        return parent is not null && await NavigateAsync(parent, hit.Id, cancellationToken);
    }

    public void SelectNode(string nodeId)
    {
        SelectedNode = Neighborhood?.Nodes.FirstOrDefault(node =>
            ExplorerIdentity.Equals(node.Id, nodeId));
        SelectedNodeDetails = null;
        _detailsCancellation?.Cancel();
        _ = RefreshSelectedNodeDetailsAsync();
        NotifyChanged();
    }

    public async Task<bool> SwitchToContextAsync(
        string? preferredFocusId = null,
        CancellationToken cancellationToken = default) =>
        await SwitchToConnectedModeAsync(ExplorerViewMode.Context, preferredFocusId, cancellationToken);

    public async Task<bool> SwitchToHybridAsync(
        string? preferredFocusId = null,
        CancellationToken cancellationToken = default) =>
        await SwitchToConnectedModeAsync(ExplorerViewMode.Hybrid, preferredFocusId, cancellationToken);

    private async Task<bool> SwitchToConnectedModeAsync(
        ExplorerViewMode targetMode,
        string? preferredFocusId,
        CancellationToken cancellationToken)
    {
        EnsureProvider();
        if (_provider is not IExplorerContextProvider)
        {
            Status = targetMode == ExplorerViewMode.Hybrid
                ? "Hybrid exploration requires OmniSorSe."
                : "Context exploration requires OmniSorSe.";
            NotifyChanged();
            return false;
        }

        var target = preferredFocusId ??
            (ViewMode == ExplorerViewMode.Structure && SelectedNode?.Kind == ExplorerNodeKind.File
                ? SelectedNode.Target
                : Neighborhood?.Focus.Target);
        if (string.IsNullOrWhiteSpace(target))
        {
            Status = $"Select an indexed file or folder before opening {targetMode}.";
            NotifyChanged();
            return false;
        }

        if (ViewMode == targetMode && ExplorerIdentity.Equals(Neighborhood?.FocusNodeId, target))
        {
            return true;
        }

        if (ViewMode == ExplorerViewMode.Structure)
        {
            _structureReturnTarget = _navigation.CurrentPath;
            _structureReturnSelectionId = SelectedNode?.Id;
            _hybridStructureSnapshot = _currentSnapshot;
            _connectedHistory.Clear();
        }

        if (_currentContextSnapshot is not null &&
            ViewMode != ExplorerViewMode.Structure &&
            ExplorerIdentity.Equals(_currentContextSnapshot.Focus.Id, target))
        {
            if (targetMode == ExplorerViewMode.Hybrid &&
                !StructureContainsFocus(_hybridStructureSnapshot, target))
            {
                return await LoadConnectedSceneAsync(
                    target,
                    targetMode,
                    pushHistory: true,
                    cancellationToken);
            }

            var previous = new ConnectedHistoryEntry(ViewMode, Neighborhood!.FocusNodeId);
            var retainedSnapshot = targetMode == ExplorerViewMode.Hybrid
                ? MergeHybridStructure(_currentContextSnapshot, _hybridStructureSnapshot!)
                : _currentContextSnapshot;
            ApplyConnectedSnapshot(retainedSnapshot, targetMode);
            _connectedHistory.Add(previous);
            NotifyChanged();
            return true;
        }

        return await LoadConnectedSceneAsync(
            target,
            targetMode,
            pushHistory: ViewMode != ExplorerViewMode.Structure,
            cancellationToken);
    }

    public async Task<bool> SwitchToStructureAsync(CancellationToken cancellationToken = default)
    {
        EnsureProvider();
        if (ViewMode == ExplorerViewMode.Structure)
        {
            return true;
        }

        var target = _structureReturnTarget ?? _navigation.CurrentPath ?? AccessRoot;
        var selection = _structureReturnSelectionId;
        var previousMode = ViewMode;
        ViewMode = ExplorerViewMode.Structure;
        _connectedHistory.Clear();
        var outcome = await LoadDirectoryAsync(
            target,
            previousContext: null,
            preferredSelectionId: selection,
            commitNavigation: null,
            showFailureScene: false,
            cancellationToken);
        if (outcome.Applied)
        {
            _currentContextSnapshot = null;
            ContextFilterSummary = null;
            Status = "Structure mode restored.";
            NotifyChanged();
        }
        else
        {
            ViewMode = previousMode;
        }

        return outcome.Applied;
    }

    public Task<bool> FocusContextNodeAsync(
        string nodeId,
        CancellationToken cancellationToken = default) =>
        FocusConnectedNodeAsync(nodeId, ExplorerViewMode.Context, cancellationToken);

    public Task<bool> FocusHybridNodeAsync(
        string nodeId,
        CancellationToken cancellationToken = default) =>
        FocusConnectedNodeAsync(nodeId, ExplorerViewMode.Hybrid, cancellationToken);

    private Task<bool> FocusConnectedNodeAsync(
        string nodeId,
        ExplorerViewMode targetMode,
        CancellationToken cancellationToken) =>
        LoadConnectedSceneAsync(nodeId, targetMode, pushHistory: true, cancellationToken);

    public bool ApplyContextFilter(ContextFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (ViewMode == ExplorerViewMode.Structure || _currentContextSnapshot is null)
        {
            return false;
        }

        ContextFilter = filter.Normalize();
        var result = BuildConnectedNeighborhood(_currentContextSnapshot, ViewMode);
        Neighborhood = result.Neighborhood;
        ContextFilterSummary = result.Summary;
        if (SelectedNode is null ||
            !Neighborhood.Nodes.Any(node => ExplorerIdentity.Equals(node.Id, SelectedNode.Id)))
        {
            SelectedNode = Neighborhood.Focus;
            SelectedNodeDetails = null;
            _detailsCancellation?.Cancel();
            _ = RefreshSelectedNodeDetailsAsync();
        }

        UpdateHighlights(notify: false);
        UpdateConnectedStatus();
        NotifyChanged();
        return true;
    }

    public bool ClearContextFilter() => ApplyContextFilter(ContextFilter.None);

    public async Task RefreshSelectedNodeDetailsAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null || _provider is not IExplorerNodeDetailsProvider detailsProvider)
        {
            return;
        }

        _detailsCancellation?.Cancel();
        _detailsCancellation?.Dispose();
        _detailsCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestVersion = Interlocked.Increment(ref _detailsRequestVersion);
        var nodeId = SelectedNode.Id;
        try
        {
            var details = await detailsProvider.GetNodeDetailsAsync(nodeId, _detailsCancellation.Token);
            if (requestVersion != Volatile.Read(ref _detailsRequestVersion) ||
                !ExplorerIdentity.Equals(SelectedNode?.Id, nodeId))
            {
                (_provider as IExplorerProviderDiagnostics)?.ReportStaleResponseRejected();
                return;
            }

            SelectedNodeDetails = details;
            NotifyChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            if (requestVersion == Volatile.Read(ref _detailsRequestVersion))
            {
                Status = "Connected node details are temporarily unavailable.";
                ProviderFailed?.Invoke(exception);
                NotifyChanged();
            }
        }
    }

    public void Reset(string status = "Choose a folder to begin. Only that location will be accessible.")
    {
        CancelActiveOperations(reportCancellation: false);
        Interlocked.Increment(ref _providerGeneration);
        _provider = null;
        _searchProvider = null;
        _navigation.Clear();
        Neighborhood = null;
        SelectedNode = null;
        SelectedNodeDetails = null;
        SearchResult = null;
        SearchQuery = string.Empty;
        HighlightedNodeIds = new HashSet<string>();
        _currentSnapshot = null;
        _currentContextSnapshot = null;
        _hybridStructureSnapshot = null;
        _previousContext = null;
        _aggregatePage = null;
        _connectedHistory.Clear();
        _structureReturnTarget = null;
        _structureReturnSelectionId = null;
        ContextFilter = ContextFilter.None;
        ContextFilterSummary = null;
        ViewMode = ExplorerViewMode.Structure;
        LoadState = ExplorerLoadState.Idle;
        IsSearching = false;
        LoadedItemCount = 0;
        Status = status;
        NotifyChanged();
    }

    public void ClearSearch()
    {
        _searchCancellation?.Cancel();
        Interlocked.Increment(ref _searchRequestVersion);
        SearchQuery = string.Empty;
        SearchResult = null;
        HighlightedNodeIds = new HashSet<string>();
        IsSearching = false;
        Status = "Search cleared.";
        NotifyChanged();
    }

    public void CancelOperations() => CancelActiveOperations(reportCancellation: true);

    public void Dispose()
    {
        CancelActiveOperations(reportCancellation: false);
        _loadCancellation?.Dispose();
        _searchCancellation?.Dispose();
        _detailsCancellation?.Dispose();
    }

    private async Task<bool> LoadConnectedSceneAsync(
        string nodeId,
        ExplorerViewMode targetMode,
        bool pushHistory,
        CancellationToken cancellationToken)
    {
        if (_provider is not IExplorerContextProvider contextProvider)
        {
            Status = targetMode == ExplorerViewMode.Hybrid
                ? "Hybrid exploration requires OmniSorSe."
                : "Context exploration requires OmniSorSe.";
            NotifyChanged();
            return false;
        }

        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestVersion = Interlocked.Increment(ref _loadRequestVersion);
        var previousMode = ViewMode;
        var previousFocusId = Neighborhood?.FocusNodeId;
        var backup = new SceneBackup(
            Neighborhood,
            SelectedNode,
            _currentSnapshot,
            _currentContextSnapshot,
            ContextFilterSummary,
            _previousContext,
            _aggregatePage);
        var stopwatch = Stopwatch.StartNew();
        LoadState = ExplorerLoadState.Loading;
        LoadedItemCount = 0;
        Status = $"Requesting bounded OmniSorSe {targetMode} data…";
        NotifyChanged();

        try
        {
            var snapshot = await contextProvider.GetContextAsync(nodeId, _loadCancellation.Token);
            if (targetMode == ExplorerViewMode.Hybrid)
            {
                snapshot = await PrepareHybridSnapshotAsync(snapshot, _loadCancellation.Token);
            }

            if (requestVersion != Volatile.Read(ref _loadRequestVersion))
            {
                (_provider as IExplorerProviderDiagnostics)?.ReportStaleResponseRejected();
                return false;
            }

            var result = BuildConnectedNeighborhood(snapshot, targetMode);
            var neighborhood = result.Neighborhood;
            if (pushHistory &&
                previousMode != ExplorerViewMode.Structure &&
                previousFocusId is not null &&
                (previousMode != targetMode ||
                 !ExplorerIdentity.Equals(previousFocusId, neighborhood.FocusNodeId)))
            {
                _connectedHistory.Add(new ConnectedHistoryEntry(previousMode, previousFocusId));
            }

            ViewMode = targetMode;
            Neighborhood = neighborhood;
            _currentContextSnapshot = snapshot;
            ContextFilterSummary = result.Summary;
            SelectedNode = neighborhood.Focus;
            SelectedNodeDetails = null;
            _currentSnapshot = null;
            _previousContext = null;
            _aggregatePage = null;
            LoadedItemCount = Math.Max(0, neighborhood.Nodes.Count - 1);
            LoadState = ExplorerLoadState.Ready;
            UpdateConnectedStatus();

            _detailsCancellation?.Cancel();
            _ = RefreshSelectedNodeDetailsAsync(CancellationToken.None);
            UpdateHighlights(notify: false);
            NotifyChanged();
            return true;
        }
        catch (OperationCanceledException) when (requestVersion == Volatile.Read(ref _loadRequestVersion))
        {
            LoadState = ExplorerLoadState.Cancelled;
            Status = $"{targetMode} loading cancelled.";
            NotifyChanged();
            throw;
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            RestoreScene(backup);
            ViewMode = previousMode;
            LoadState = ExplorerLoadState.Failed;
            Status = targetMode == ExplorerViewMode.Hybrid && previousMode == ExplorerViewMode.Structure
                ? "OmniSorSe could not load the Context layer. The structural graph is retained."
                : $"OmniSorSe could not complete the {targetMode} request. The previous graph is retained.";
            ProviderFailed?.Invoke(exception);
            NotifyChanged();
            return false;
        }
        finally
        {
            stopwatch.Stop();
            if (requestVersion == Volatile.Read(ref _loadRequestVersion))
            {
                LastLoadDuration = stopwatch.Elapsed;
            }
        }
    }

    private async Task<LoadOutcome> LoadDirectoryAsync(
        string path,
        ExplorerEntry? previousContext,
        string? preferredSelectionId,
        Action? commitNavigation,
        bool showFailureScene,
        CancellationToken cancellationToken)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestVersion = Interlocked.Increment(ref _loadRequestVersion);
        var stopwatch = Stopwatch.StartNew();
        var backup = new SceneBackup(
            Neighborhood,
            SelectedNode,
            _currentSnapshot,
            _currentContextSnapshot,
            ContextFilterSummary,
            _previousContext,
            _aggregatePage);
        var children = new List<ExplorerEntry>();
        var navigationCommitted = commitNavigation is null;
        _aggregatePage = null;
        LoadState = ExplorerLoadState.Loading;
        LoadedItemCount = 0;
        Status = "Opening the bounded graph shell…";
        NotifyChanged();

        try
        {
            if (_provider is IProgressiveExplorerProvider progressiveProvider)
            {
                await foreach (var batch in progressiveProvider.GetDirectoryBatchesAsync(
                                   path,
                                   ProgressiveBatchSize,
                                   _loadCancellation.Token))
                {
                    if (requestVersion != Volatile.Read(ref _loadRequestVersion))
                    {
                        (_provider as IExplorerProviderDiagnostics)?.ReportStaleResponseRejected();
                        return LoadOutcome.Obsolete;
                    }

                    children.AddRange(batch.AddedChildren);
                    var snapshot = ToSnapshot(batch, children);
                    var canCommit = batch.Failure == ExplorerFailureKind.None &&
                        (children.Count > 0 || batch.IsComplete);
                    if (!navigationCommitted && canCommit)
                    {
                        commitNavigation!();
                        navigationCommitted = true;
                    }

                    if (batch.Failure != ExplorerFailureKind.None && children.Count == 0 && !showFailureScene)
                    {
                        RestoreScene(backup);
                        LoadState = ExplorerLoadState.Failed;
                        Status = batch.Warning ?? "The folder could not be read.";
                        return new LoadOutcome(false, batch.Failure);
                    }

                    ApplySnapshot(snapshot, previousContext, preferredSelectionId);
                    LoadedItemCount = children.Count;
                    LoadState = batch.IsComplete
                        ? batch.Failure == ExplorerFailureKind.None
                            ? ExplorerLoadState.Ready
                            : ExplorerLoadState.Failed
                        : children.Count == 0
                            ? ExplorerLoadState.Loading
                            : ExplorerLoadState.PartiallyLoaded;
                    UpdateLoadStatus(snapshot, batch.IsComplete);
                    NotifyChanged();
                }
            }
            else
            {
                var snapshot = await _provider!.GetDirectoryAsync(path, _loadCancellation.Token);
                if (requestVersion != Volatile.Read(ref _loadRequestVersion))
                {
                    (_provider as IExplorerProviderDiagnostics)?.ReportStaleResponseRejected();
                    return LoadOutcome.Obsolete;
                }

                if (snapshot.Failure != ExplorerFailureKind.None && !showFailureScene)
                {
                    RestoreScene(backup);
                    LoadState = ExplorerLoadState.Failed;
                    Status = snapshot.Warning ?? "The folder could not be read.";
                    return new LoadOutcome(false, snapshot.Failure);
                }

                if (!navigationCommitted && snapshot.Failure == ExplorerFailureKind.None)
                {
                    commitNavigation!();
                    navigationCommitted = true;
                }

                ApplySnapshot(snapshot, previousContext, preferredSelectionId);
                LoadedItemCount = snapshot.Children.Count;
                LoadState = snapshot.Failure == ExplorerFailureKind.None
                    ? ExplorerLoadState.Ready
                    : ExplorerLoadState.Failed;
                UpdateLoadStatus(snapshot, isComplete: true);
                NotifyChanged();
            }

            return new LoadOutcome(_currentSnapshot is not null, _currentSnapshot?.Failure ?? ExplorerFailureKind.None);
        }
        catch (OperationCanceledException) when (requestVersion == Volatile.Read(ref _loadRequestVersion))
        {
            LoadState = ExplorerLoadState.Cancelled;
            Status = "Folder loading cancelled.";
            NotifyChanged();
            throw;
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            RestoreScene(backup);
            LoadState = ExplorerLoadState.Failed;
            Status = ProviderMode == ExplorerProviderMode.Connected
                ? "OmniSorSe disconnected or could not complete the request. The previous graph is retained."
                : "The folder could not be read.";
            ProviderFailed?.Invoke(exception);
            NotifyChanged();
            return new LoadOutcome(false, ExplorerFailureKind.EnumerationFailed);
        }
        finally
        {
            stopwatch.Stop();
            if (requestVersion == Volatile.Read(ref _loadRequestVersion))
            {
                LastLoadDuration = stopwatch.Elapsed;
            }
        }
    }

    private void ApplySnapshot(
        ExplorerDirectorySnapshot snapshot,
        ExplorerEntry? previousContext,
        string? preferredSelectionId)
    {
        _currentSnapshot = snapshot;
        _currentContextSnapshot = null;
        _hybridStructureSnapshot = snapshot;
        ContextFilterSummary = null;
        _previousContext = previousContext;
        Neighborhood = _neighborhoodBuilder.Build(snapshot, previousContext, preferredSelectionId, _aggregatePage);
        SelectedNode = preferredSelectionId is null
            ? Neighborhood.Focus
            : Neighborhood.Nodes.FirstOrDefault(node =>
                ExplorerIdentity.Equals(node.Id, preferredSelectionId)) ?? Neighborhood.Focus;
        SelectedNodeDetails = null;
        _detailsCancellation?.Cancel();
        _ = RefreshSelectedNodeDetailsAsync();
        UpdateHighlights(notify: false);
    }

    private void RebuildCurrentScene()
    {
        if (_currentSnapshot is null)
        {
            return;
        }

        Neighborhood = _neighborhoodBuilder.Build(_currentSnapshot, _previousContext, aggregatePage: _aggregatePage);
    }

    private void RestoreScene(SceneBackup backup)
    {
        Neighborhood = backup.Neighborhood;
        SelectedNode = backup.SelectedNode;
        _currentSnapshot = backup.Snapshot;
        _currentContextSnapshot = backup.ContextSnapshot;
        ContextFilterSummary = backup.FilterSummary;
        _previousContext = backup.PreviousContext;
        _aggregatePage = backup.AggregatePage;
        UpdateHighlights(notify: false);
    }

    private void UpdateLoadStatus(ExplorerDirectorySnapshot snapshot, bool isComplete)
    {
        if (snapshot.Failure != ExplorerFailureKind.None)
        {
            Status = snapshot.Warning ?? "The folder could not be read.";
            return;
        }

        if (!isComplete)
        {
            Status = LoadedItemCount == 0
                ? "Graph shell ready · reading structural items…"
                : $"Graph interactive · {LoadedItemCount:N0} items streamed…";
            return;
        }

        var total = Neighborhood?.TotalChildCount ?? snapshot.Children.Count;
        var visible = Math.Max(0, (Neighborhood?.Nodes.Count ?? 1) - 1);
        Status = total == 0
            ? ProviderMode == ExplorerProviderMode.Connected
                ? "This authorized indexed folder is empty · focus remains visible"
                : "This folder is empty · focus remains visible"
            : ProviderMode == ExplorerProviderMode.Connected
                ? $"{total:N0} indexed items · {visible:N0} graph nodes visible"
                : $"{total:N0} items · {visible:N0} graph nodes visible";
        if (!string.IsNullOrWhiteSpace(snapshot.Warning))
        {
            Status += $" · {snapshot.Warning}";
        }
    }

    private async Task<ExplorerContextSnapshot> PrepareHybridSnapshotAsync(
        ExplorerContextSnapshot contextSnapshot,
        CancellationToken cancellationToken)
    {
        var structureSnapshot = _hybridStructureSnapshot;
        if (!StructureContainsFocus(structureSnapshot, contextSnapshot.Focus.Id))
        {
            var structureTarget = contextSnapshot.Focus.ParentNavigationTarget ?? contextSnapshot.Focus.Target;
            try
            {
                structureSnapshot = await _provider!.GetDirectoryAsync(structureTarget, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IsProviderFailure(exception))
            {
                ProviderFailed?.Invoke(exception);
                return AppendHybridWarning(
                    contextSnapshot,
                    "Structural orientation could not be refreshed; the authoritative Context layer remains available.");
            }

            if (structureSnapshot.Failure != ExplorerFailureKind.None)
            {
                return AppendHybridWarning(
                    contextSnapshot,
                    structureSnapshot.Warning ??
                    "Structural orientation could not be refreshed; the authoritative Context layer remains available.");
            }

            _hybridStructureSnapshot = structureSnapshot;
        }

        return MergeHybridStructure(contextSnapshot, structureSnapshot!);
    }

    private static bool StructureContainsFocus(
        ExplorerDirectorySnapshot? structureSnapshot,
        string focusId) => structureSnapshot is not null &&
        (ExplorerIdentity.Equals(structureSnapshot.Focus.Id, focusId) ||
         structureSnapshot.Children.Any(child => ExplorerIdentity.Equals(child.Id, focusId)));

    private static ExplorerContextSnapshot MergeHybridStructure(
        ExplorerContextSnapshot contextSnapshot,
        ExplorerDirectorySnapshot structureSnapshot)
    {
        var nodes = contextSnapshot.Nodes
            .Concat([structureSnapshot.Focus])
            .Concat(structureSnapshot.Children)
            .Prepend(contextSnapshot.Focus)
            .GroupBy(node => node.Id, ExplorerIdentity.Comparer)
            .Select(group => group.First())
            .ToArray();
        var structuralEdges = contextSnapshot.StructuralEdges
            .Concat(structureSnapshot.Children.Select(child => new ExplorerEdge(
                structureSnapshot.Focus.Id,
                child.Id,
                ExplorerGraphEdgeKind.Structural)))
            .GroupBy(edge => $"{edge.SourceId}\u001f{edge.TargetId}", ExplorerIdentity.Comparer)
            .Select(group => group.First())
            .ToArray();
        var warning = string.Join(
            " ",
            new[] { contextSnapshot.Warning, structureSnapshot.Warning }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return contextSnapshot with
        {
            Nodes = nodes,
            StructuralEdges = structuralEdges,
            WasTruncated = contextSnapshot.WasTruncated || structureSnapshot.WasTruncated,
            Warning = string.IsNullOrWhiteSpace(warning) ? null : warning,
        };
    }

    private static ExplorerContextSnapshot AppendHybridWarning(
        ExplorerContextSnapshot snapshot,
        string warning) => snapshot with
        {
            Warning = string.IsNullOrWhiteSpace(snapshot.Warning)
            ? warning
            : $"{snapshot.Warning} {warning}",
        };

    private ContextNeighborhoodBuildResult BuildConnectedNeighborhood(
        ExplorerContextSnapshot snapshot,
        ExplorerViewMode viewMode) => viewMode == ExplorerViewMode.Hybrid
        ? _hybridBuilder.BuildDetailed(snapshot, ContextFilter)
        : _contextBuilder.BuildDetailed(snapshot, ContextFilter);

    private void ApplyConnectedSnapshot(ExplorerContextSnapshot snapshot, ExplorerViewMode viewMode)
    {
        var result = BuildConnectedNeighborhood(snapshot, viewMode);
        ViewMode = viewMode;
        Neighborhood = result.Neighborhood;
        _currentContextSnapshot = snapshot;
        ContextFilterSummary = result.Summary;
        SelectedNode = Neighborhood.Focus;
        SelectedNodeDetails = null;
        _currentSnapshot = null;
        _previousContext = null;
        _aggregatePage = null;
        LoadedItemCount = Math.Max(0, Neighborhood.Nodes.Count - 1);
        LoadState = ExplorerLoadState.Ready;
        _detailsCancellation?.Cancel();
        _ = RefreshSelectedNodeDetailsAsync(CancellationToken.None);
        UpdateHighlights(notify: false);
        UpdateConnectedStatus();
    }

    private void UpdateConnectedStatus()
    {
        if (Neighborhood is null || ContextFilterSummary is null)
        {
            return;
        }

        var summary = ContextFilterSummary;
        if (summary.MatchingRelationshipCount == 0)
        {
            Status = ContextFilter.IsActive
                ? ViewMode == ExplorerViewMode.Hybrid
                    ? "No relationships match the current Context filters. Structural orientation remains available; clear filters to restore the authoritative Context layer."
                    : "No relationships match the current Context filters. Clear filters to restore the authoritative neighborhood."
                : ViewMode == ExplorerViewMode.Hybrid
                    ? "No contextual relationships found for this item. Structural orientation remains available."
                    : "No contextual relationships found for this item.";
            return;
        }

        var modeDescription = ViewMode == ExplorerViewMode.Hybrid
            ? $"{Neighborhood.Nodes.Count(node => (node.Roles & ExplorerNodeRole.Structural) != 0):N0} structural / {Neighborhood.Nodes.Count(node => (node.Roles & ExplorerNodeRole.Contextual) != 0):N0} contextual nodes"
            : $"{Neighborhood.Nodes.Count:N0} Context nodes";
        Status = $"{summary.VisibleRelationshipCount:N0} of {summary.MatchingRelationshipCount:N0} matching relationships visible · {modeDescription}";
        if (summary.HiddenMatchingRelationshipCount > 0)
        {
            Status += " · bounded by the focus-local Context budget";
        }

        if (ContextFilter.IsActive)
        {
            Status += " · filters active";
        }

        if (!string.IsNullOrWhiteSpace(Neighborhood.Warning))
        {
            Status += $" · {Neighborhood.Warning}";
        }
    }

    private void UpdateHighlights(bool notify)
    {
        if (Neighborhood is null || SearchResult is null)
        {
            HighlightedNodeIds = new HashSet<string>();
        }
        else
        {
            var visibleIds = Neighborhood.Nodes.Select(node => node.Id).ToHashSet(ExplorerIdentity.Comparer);
            HighlightedNodeIds = SearchResult.Hits
                .Select(hit => hit.Id)
                .Where(visibleIds.Contains)
                .ToHashSet(ExplorerIdentity.Comparer);
        }

        if (notify)
        {
            NotifyChanged();
        }
    }

    private void CancelActiveOperations(bool reportCancellation)
    {
        _loadCancellation?.Cancel();
        _searchCancellation?.Cancel();
        _detailsCancellation?.Cancel();
        Interlocked.Increment(ref _loadRequestVersion);
        Interlocked.Increment(ref _searchRequestVersion);
        Interlocked.Increment(ref _detailsRequestVersion);

        if (!reportCancellation)
        {
            return;
        }

        if (IsLoading)
        {
            LoadState = ExplorerLoadState.Cancelled;
            Status = "Folder loading cancelled.";
        }

        if (IsSearching)
        {
            IsSearching = false;
            Status = "Search cancelled.";
        }

        NotifyChanged();
    }

    private static ExplorerDirectorySnapshot ToSnapshot(
        ExplorerDirectoryBatch batch,
        IReadOnlyList<ExplorerEntry> children) => new(
            batch.Focus,
            children.ToArray(),
            batch.Failure,
            batch.Warning,
            batch.TotalChildCount ?? batch.ItemsObserved,
            batch.WasTruncated);

    private static ExplorerEntry? ToEntry(ExplorerNode? node) => node is null
        ? null
        : new ExplorerEntry(
            node.Id,
            node.Name,
            node.Path,
            ExplorerNodeKind.Folder,
            node.SizeBytes,
            node.LastModified,
            false,
            true,
            node.Target);

    private void EnsureProvider()
    {
        if (_provider is null || _searchProvider is null)
        {
            throw new InvalidOperationException("Choose a folder before exploring.");
        }
    }

    private bool IsProviderFailure(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        InvalidDataException or
        TimeoutException ||
        (_provider as IExplorerProviderDiagnostics)?.IsProviderFailure(exception) is true;

    private void NotifyChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private sealed record LoadOutcome(bool Applied, ExplorerFailureKind Failure)
    {
        public static LoadOutcome Obsolete { get; } = new(false, ExplorerFailureKind.None);
    }

    private sealed record SceneBackup(
        ExplorerNeighborhood? Neighborhood,
        ExplorerNode? SelectedNode,
        ExplorerDirectorySnapshot? Snapshot,
        ExplorerContextSnapshot? ContextSnapshot,
        ContextFilterSummary? FilterSummary,
        ExplorerEntry? PreviousContext,
        AggregatePage? AggregatePage);

    private sealed record ConnectedHistoryEntry(ExplorerViewMode ViewMode, string FocusNodeId);
}
