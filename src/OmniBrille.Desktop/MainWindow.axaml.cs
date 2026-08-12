using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using OmniBrille.Core;
using OmniBrille.Desktop.Presentation;
using OmniBrille.Infrastructure;

namespace OmniBrille.Desktop;

public sealed partial class MainWindow : Window, IDisposable
{
    private readonly ExplorerSession _session;
    private readonly IVisualPreferencesStore _preferencesStore;
    private readonly DispatcherTimer _diagnosticsTimer;
    private VisualPreferences _preferences;
    private ExplorerNeighborhood? _lastRenderedNeighborhood;
    private bool _detailsDismissed;
    private bool _isApplyingPreferences;
    private bool _isDisposed;

    public MainWindow()
        : this(new ExplorerSession(), new JsonVisualPreferencesStore(), null, null)
    {
    }

    public MainWindow(string? startupRoot, string? startupTheme)
        : this(new ExplorerSession(), new JsonVisualPreferencesStore(), startupRoot, startupTheme)
    {
    }

    public MainWindow(
        ExplorerSession session,
        IVisualPreferencesStore preferencesStore,
        string? startupRoot = null,
        string? startupTheme = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
        _preferences = _preferencesStore.Load().Normalize();
        if (!string.IsNullOrWhiteSpace(startupTheme))
        {
            _preferences = (_preferences with { Theme = startupTheme }).Normalize();
        }

        _isApplyingPreferences = true;
        try
        {
            InitializeComponent();
        }
        finally
        {
            _isApplyingPreferences = false;
        }

        _session.StateChanged += OnSessionStateChanged;
        GraphScene.NodeSelected += OnGraphNodeSelected;
        GraphScene.NodeActivated += OnGraphNodeActivated;
        GraphScene.BackRequested += async (_, _) => await GoBackAsync();
        GraphScene.DismissRequested += (_, _) => DismissTransientSurfaces();
        KeyDown += OnWindowKeyDown;
        Closed += (_, _) => Dispose();

        _diagnosticsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _diagnosticsTimer.Tick += (_, _) => UpdateDiagnostics();
        _diagnosticsTimer.Start();

        ApplyPreferencesToControls();
        UpdateView();

        if (!string.IsNullOrWhiteSpace(startupRoot))
        {
            Opened += async (_, _) => await OpenRootAsync(startupRoot);
        }
    }

    public ExplorerSession Session => _session;

    public VisualPreferences Preferences => _preferences;

    private async void OnChooseFolderClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            SetTransientStatus("The operating-system folder picker is unavailable.");
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose an OmniBrille access root",
            AllowMultiple = false,
        });
        var selected = folders.Count > 0 ? folders[0] : null;
        if (selected is null)
        {
            return;
        }

        var path = selected.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            SetTransientStatus("That location does not expose a local filesystem path.");
            return;
        }

        await OpenRootAsync(path);
    }

    private async void OnBackClick(object? sender, RoutedEventArgs e) => await GoBackAsync();

    private async Task GoBackAsync()
    {
        try
        {
            await _session.GoBackAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs e) => await RunSearchAsync();

    private async void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await RunSearchAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClearSearch();
            GraphScene.Focus();
            e.Handled = true;
        }
    }

    private async Task RunSearchAsync()
    {
        if (string.IsNullOrEmpty(_session.AccessRoot))
        {
            SetTransientStatus("Choose a folder before searching.");
            return;
        }

        await _session.SearchAsync(SearchBox.Text ?? string.Empty);
    }

    private async Task OpenRootAsync(string path)
    {
        try
        {
            var provider = new FileSystemExplorerProvider(path);
            await _session.OpenRootAsync(provider, provider);
            GraphScene.ResetView();
        }
        catch (OperationCanceledException)
        {
            SetTransientStatus("Folder loading cancelled.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            SetTransientStatus($"The selected folder could not be opened: {exception.Message}");
        }
    }

    private async void OnSearchResultDoubleTapped(object? sender, TappedEventArgs e) =>
        await FocusSelectedSearchResultAsync();

    private async void OnSearchResultsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await FocusSelectedSearchResultAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClearSearch();
            GraphScene.Focus();
            e.Handled = true;
        }
    }

    private async void OnFocusSearchResultClick(object? sender, RoutedEventArgs e) =>
        await FocusSelectedSearchResultAsync();

    private async Task FocusSelectedSearchResultAsync()
    {
        if (SearchResultsList.SelectedItem is not ExplorerSearchHit hit)
        {
            SetTransientStatus("Select a search result first.");
            return;
        }

        try
        {
            await _session.FocusSearchHitAsync(hit);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedIndex = sender is ComboBox picker ? picker.SelectedIndex : 0;
        var theme = selectedIndex == 1 ? "Light" : "Dark";
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = selectedIndex == 1
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
        }

        if (!_isApplyingPreferences)
        {
            _preferences = _preferences with { Theme = theme };
            _preferencesStore.Save(_preferences);
        }

        GraphScene?.InvalidateVisual();
    }

    private void OnVisualPreferenceChanged(object? sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        _preferences = _preferences with
        {
            ReducedMotion = ReducedMotionToggle.IsChecked == true,
            ReducedEffects = ReducedEffectsToggle.IsChecked == true,
            DiagnosticsVisible = DiagnosticsToggle.IsChecked == true,
        };
        _preferencesStore.Save(_preferences);
        ApplyVisualPreferences();
        UpdateView();
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        SettingsPanel.IsVisible = !SettingsPanel.IsVisible;
        if (SettingsPanel.IsVisible)
        {
            _detailsDismissed = true;
            DetailsPanel.IsVisible = false;
            ReducedMotionToggle.Focus();
        }
        else
        {
            SettingsButton.Focus();
        }
    }

    private void OnCloseDetailsClick(object? sender, RoutedEventArgs e)
    {
        _detailsDismissed = true;
        DetailsPanel.IsVisible = false;
        GraphScene.Focus();
    }

    private void OnCloseSearchClick(object? sender, RoutedEventArgs e)
    {
        ClearSearch();
        GraphScene.Focus();
    }

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => GraphScene.ZoomIn();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => GraphScene.ZoomOut();

    private void OnResetViewClick(object? sender, RoutedEventArgs e) => GraphScene.ResetView();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _session.CancelOperations();

    private void OnGraphNodeSelected(object? sender, string nodeId)
    {
        _detailsDismissed = false;
        SettingsPanel.IsVisible = false;
        _session.SelectNode(nodeId);
    }

    private async void OnGraphNodeActivated(object? sender, string nodeId)
    {
        var node = _session.Neighborhood?.Nodes.FirstOrDefault(item =>
            StringComparer.OrdinalIgnoreCase.Equals(item.Id, nodeId));
        if (node is null)
        {
            return;
        }

        if (node.Kind == ExplorerNodeKind.Aggregate)
        {
            if (!_session.ActivateAggregate(node.Id))
            {
                SetTransientStatus("This aggregate is informational because the source enumeration was bounded.");
            }

            return;
        }

        if (node.Kind is ExplorerNodeKind.Folder or ExplorerNodeKind.Context && node.IsNavigable)
        {
            try
            {
                await _session.NavigateAsync(node.Path);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if ((e.Key == Key.Left && e.KeyModifiers.HasFlag(KeyModifiers.Alt)) || e.Key == Key.BrowserBack)
        {
            _ = GoBackAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DismissTransientSurfaces();
            e.Handled = true;
        }
    }

    private void OnSessionStateChanged(object? sender, EventArgs e) => UpdateView();

    private void UpdateView()
    {
        var neighborhood = _session.Neighborhood;
        var selected = _session.SelectedNode;
        CurrentPathText.Text = string.IsNullOrEmpty(_session.CurrentPath)
            ? "No folder selected"
            : _session.CurrentPath;
        StatusText.Text = _session.Status;
        BackButton.IsEnabled = _session.CanGoBack && !_session.IsLoading;
        WelcomePanel.IsVisible = neighborhood is null && !_session.IsLoading;

        var initialLoading = _session.IsLoading && neighborhood is null;
        InitialLoadingOverlay.IsVisible = initialLoading;
        InitialLoadingText.Text = _session.Status;
        DataRain.SetActive(initialLoading);
        ProgressHud.IsVisible = (neighborhood is not null && _session.IsLoading) || _session.IsSearching;
        ProgressText.Text = _session.IsSearching
            ? "Searching selected root…"
            : _session.LoadState == ExplorerLoadState.PartiallyLoaded
                ? $"Graph interactive · {_session.LoadedItemCount:N0} items streamed"
                : "Reading structural items…";

        DetailsPanel.IsVisible = selected is not null && !_detailsDismissed && !SettingsPanel.IsVisible;
        if (selected is not null)
        {
            UpdateDetails(selected, neighborhood);
        }

        var results = _session.SearchResult;
        SearchResultsList.ItemsSource = results?.Hits;
        SearchResultsPanel.IsVisible = results is not null && _session.SearchQuery.Length > 0;
        SearchSummaryText.Text = results is null
            ? "Search results"
            : $"{results.Hits.Count:N0} MATCHES{(results.WasTruncated ? " · BOUNDED" : string.Empty)}";

        GraphScene.ReducedMotion = _preferences.ReducedMotion;
        GraphScene.ReducedEffects = _preferences.ReducedEffects;
        GraphScene.SearchActive = results is not null && _session.SearchQuery.Length > 0;
        DataRain.ReducedMotion = _preferences.ReducedMotion;
        DataRain.ReducedEffects = _preferences.ReducedEffects;
        var neighborhoodChanged = !ReferenceEquals(neighborhood, _lastRenderedNeighborhood);
        GraphScene.SetScene(
            neighborhood,
            selected?.Id,
            _session.HighlightedNodeIds,
            animate: neighborhoodChanged && !_preferences.ReducedMotion);
        _lastRenderedNeighborhood = neighborhood;
        DiagnosticsPanel.IsVisible = _preferences.DiagnosticsVisible;
        UpdateDiagnostics();
    }

    private void UpdateDetails(ExplorerNode selected, ExplorerNeighborhood? neighborhood)
    {
        DetailsNameText.Text = selected.Name;
        DetailsTypeText.Text = selected.Kind switch
        {
            ExplorerNodeKind.Folder => "Folder",
            ExplorerNodeKind.File => "File",
            ExplorerNodeKind.Aggregate => selected.AggregateAction is null ? "Bounded aggregate" : "Refinable aggregate",
            ExplorerNodeKind.Context => "Previous focus",
            _ => selected.Kind.ToString(),
        };
        DetailsSizeText.Text = selected.Kind == ExplorerNodeKind.Aggregate
            ? $"{selected.AggregatedItemCount:N0} items"
            : selected.Id == neighborhood?.FocusNodeId
                ? $"{neighborhood.TotalChildCount:N0} children"
                : FormatSize(selected.SizeBytes);
        DetailsModifiedText.Text = selected.LastModified?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "—";
        DetailsAccessText.Text = selected.IsNavigable
            ? "Navigable"
            : selected.Kind == ExplorerNodeKind.File
                ? "Read-only metadata"
                : "Bounded / informational";
        DetailsPathText.Text = selected.Path;
        DetailsHintText.Text = selected.Kind == ExplorerNodeKind.Aggregate
            ? selected.AggregateAction?.Description ?? "Enumeration was bounded before this aggregate could be refined."
            : "Double-click a folder to move it into focus. Reparse-point folders are shown but never traversed.";
    }

    private void ApplyPreferencesToControls()
    {
        _isApplyingPreferences = true;
        try
        {
            ThemePicker.SelectedIndex = _preferences.Theme == "Light" ? 1 : 0;
            ReducedMotionToggle.IsChecked = _preferences.ReducedMotion;
            ReducedEffectsToggle.IsChecked = _preferences.ReducedEffects;
            DiagnosticsToggle.IsChecked = _preferences.DiagnosticsVisible;
        }
        finally
        {
            _isApplyingPreferences = false;
        }

        ApplyVisualPreferences();
    }

    private void ApplyVisualPreferences()
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = _preferences.Theme == "Light"
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
        }

        GraphScene.ReducedMotion = _preferences.ReducedMotion;
        GraphScene.ReducedEffects = _preferences.ReducedEffects;
        DataRain.ReducedMotion = _preferences.ReducedMotion;
        DataRain.ReducedEffects = _preferences.ReducedEffects;
        DiagnosticsPanel.IsVisible = _preferences.DiagnosticsVisible;
        GraphScene.InvalidateVisual();
        DataRain.InvalidateVisual();
    }

    private void UpdateDiagnostics()
    {
        if (!_preferences.DiagnosticsVisible)
        {
            return;
        }

        var diagnostics = GraphScene.Diagnostics;
        DiagnosticsText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"nodes {diagnostics.Nodes}/{_session.SceneBudget}  edges {diagnostics.Edges}  labels {diagnostics.Labels}  zoom {diagnostics.Zoom:0.00}\n" +
            $"layout {diagnostics.LayoutDuration.TotalMilliseconds:0.00} ms  prep {diagnostics.ScenePreparationDuration.TotalMilliseconds:0.00} ms  render {diagnostics.LastRenderDuration.TotalMilliseconds:0.00} ms  load {_session.LastLoadDuration.TotalMilliseconds:0.0} ms");
    }

    private void ClearSearch()
    {
        SearchBox.Text = string.Empty;
        _session.ClearSearch();
    }

    private void DismissTransientSurfaces()
    {
        if (_session.IsLoading || _session.IsSearching)
        {
            _session.CancelOperations();
        }

        SettingsPanel.IsVisible = false;
        _detailsDismissed = true;
        DetailsPanel.IsVisible = false;
        if (_session.SearchResult is not null)
        {
            ClearSearch();
        }

        GraphScene.Focus();
    }

    private void SetTransientStatus(string message) => StatusText.Text = message;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _diagnosticsTimer.Stop();
        _session.Dispose();
        _isDisposed = true;
    }

    private static string FormatSize(long? bytes)
    {
        if (bytes is null)
        {
            return "—";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
