using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using OmniBrille.Core;
using OmniBrille.Desktop.Presentation;
using OmniBrille.Infrastructure;

namespace OmniBrille.Desktop;

public sealed partial class MainWindow : Window, IDisposable
{
    private readonly ExplorerSession _session = new();
    private string? _lastRenderedFocus;
    private bool _isDisposed;

    public MainWindow()
        : this(null, null)
    {
    }

    public MainWindow(string? startupRoot, string? startupTheme)
    {
        InitializeComponent();
        _session.StateChanged += OnSessionStateChanged;
        GraphScene.NodeSelected += OnGraphNodeSelected;
        GraphScene.NodeActivated += OnGraphNodeActivated;
        GraphScene.BackRequested += async (_, _) => await GoBackAsync();
        Closed += (_, _) => Dispose();
        UpdateView();

        if (string.Equals(startupTheme, "Light", StringComparison.OrdinalIgnoreCase))
        {
            ThemePicker.SelectedIndex = 1;
        }

        if (!string.IsNullOrWhiteSpace(startupRoot))
        {
            Opened += async (_, _) => await OpenRootAsync(startupRoot);
        }
    }

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
            SearchBox.Text = string.Empty;
            await RunSearchAsync();
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
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = selectedIndex == 1
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
        }

        GraphScene?.InvalidateVisual();
    }

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => GraphScene.ZoomIn();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => GraphScene.ZoomOut();

    private void OnResetViewClick(object? sender, RoutedEventArgs e) => GraphScene.ResetView();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _session.CancelOperations();

    private void OnGraphNodeSelected(object? sender, string nodeId) => _session.SelectNode(nodeId);

    private async void OnGraphNodeActivated(object? sender, string nodeId)
    {
        var node = _session.Neighborhood?.Nodes.FirstOrDefault(item =>
            StringComparer.OrdinalIgnoreCase.Equals(item.Id, nodeId));
        if (node is null)
        {
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
        else if (node.Kind == ExplorerNodeKind.Aggregate)
        {
            SetTransientStatus("This aggregate protects the node budget. Search or drill into a child folder to refine the view.");
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
        WelcomePanel.IsVisible = neighborhood is null;
        LoadingOverlay.IsVisible = _session.IsLoading;
        DetailsPanel.IsVisible = selected is not null;

        if (selected is not null)
        {
            DetailsNameText.Text = selected.Name;
            DetailsTypeText.Text = selected.Kind switch
            {
                ExplorerNodeKind.Folder => "Folder",
                ExplorerNodeKind.File => "File",
                ExplorerNodeKind.Aggregate => "Aggregate",
                ExplorerNodeKind.Context => "Previous focus",
                _ => selected.Kind.ToString(),
            };
            DetailsSizeText.Text = selected.Kind == ExplorerNodeKind.Aggregate
                ? $"{selected.AggregatedItemCount:N0} items"
                : FormatSize(selected.SizeBytes);
            DetailsModifiedText.Text = selected.LastModified?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "—";
            DetailsPathText.Text = selected.Path;
        }

        var results = _session.SearchResult;
        SearchResultsList.ItemsSource = results?.Hits;
        SearchResultsPanel.IsVisible = results is not null && !string.IsNullOrWhiteSpace(SearchBox.Text);
        SearchSummaryText.Text = results is null
            ? "Search results"
            : $"{results.Hits.Count:N0} MATCHES{(results.WasTruncated ? " · BOUNDED" : string.Empty)}";

        var focus = neighborhood?.FocusNodeId;
        var animate = !StringComparer.OrdinalIgnoreCase.Equals(focus, _lastRenderedFocus);
        GraphScene.SetScene(neighborhood, selected?.Id, _session.HighlightedNodeIds, animate);
        _lastRenderedFocus = focus;
    }

    private void SetTransientStatus(string message) => StatusText.Text = message;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

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
