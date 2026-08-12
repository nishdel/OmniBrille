using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using OmniBrille.Core;
using OmniBrille.Desktop;
using OmniBrille.Desktop.Presentation;
using OmniBrille.Desktop.Rendering;

namespace OmniBrille.HeadlessTests;

public sealed class MainWindowHeadlessTests
{
    private readonly ITestOutputHelper _output;

    public MainWindowHeadlessTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    public void WindowCreation_ProvidesNamedAccessibleControls()
    {
        using var window = CreateWindow(out _, out _);
        window.Show();

        Assert.Equal("OmniBrille — Structure", window.Title);
        Assert.Equal("Choose an access folder", AutomationProperties.GetName(window.FindControl<Button>("ChooseFolderButton")!));
        Assert.Equal("Structural search", AutomationProperties.GetName(window.FindControl<TextBox>("SearchBox")!));
        Assert.Equal("Theme", AutomationProperties.GetName(window.FindControl<ComboBox>("ThemePicker")!));
        var graph = window.FindControl<Control>("GraphScene")!;
        Assert.Equal("Spatial folder graph", AutomationProperties.GetName(graph));
        Assert.Equal("Spatial folder graph", ControlAutomationPeer.CreatePeerForElement(graph).GetName());
    }

    [AvaloniaFact]
    public void VisualSettings_UpdateRendererAndPersistPreferences()
    {
        using var window = CreateWindow(out _, out var store);
        window.Show();
        var settingsButton = window.FindControl<Button>("SettingsButton")!;
        settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var reducedMotion = window.FindControl<CheckBox>("ReducedMotionToggle")!;
        var reducedEffects = window.FindControl<CheckBox>("ReducedEffectsToggle")!;
        reducedMotion.IsChecked = true;
        reducedEffects.IsChecked = true;

        Assert.True(window.FindControl<Border>("SettingsPanel")!.IsVisible);
        Assert.True(window.Preferences.ReducedMotion);
        Assert.True(window.Preferences.ReducedEffects);
        Assert.True(store.Saved!.ReducedMotion);
        Assert.True(store.Saved.ReducedEffects);
    }

    [AvaloniaFact]
    public void ThemeSwitching_UsesSharedLightAndDarkVariants()
    {
        using var window = CreateWindow(out _, out var store);
        window.Show();
        var theme = window.FindControl<ComboBox>("ThemePicker")!;

        theme.SelectedIndex = 1;
        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
        Assert.Equal("Light", store.Saved!.Theme);

        theme.SelectedIndex = 0;
        Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
        Assert.Equal("Dark", store.Saved!.Theme);
    }

    [AvaloniaFact]
    public void StartupTheme_IsNotOverwrittenByXamlDefaults()
    {
        var session = new ExplorerSession();
        var store = new MemoryPreferencesStore();
        using var window = new MainWindow(session, store, startupTheme: "Light");
        window.Show();

        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
        Assert.Equal("Light", window.Preferences.Theme);
        Assert.Equal(1, window.FindControl<ComboBox>("ThemePicker")!.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task SessionLoadingAndSelection_UpdateLoadingAndDetailsSurfaces()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleHeadlessRoot");
        var provider = new DeferredProvider(root);

        var opening = session.OpenRootAsync(provider, provider);

        Assert.True(window.FindControl<Border>("InitialLoadingOverlay")!.IsVisible);
        provider.Complete(new ExplorerDirectorySnapshot(
            Entry(root, ExplorerNodeKind.Folder),
            [Entry(Path.Combine(root, "child"), ExplorerNodeKind.Folder)]));
        await opening;

        Assert.False(window.FindControl<Border>("InitialLoadingOverlay")!.IsVisible);
        Assert.False(window.FindControl<Border>("WelcomePanel")!.IsVisible);
        Assert.True(window.FindControl<Border>("DetailsPanel")!.IsVisible);
        Assert.Equal(root, window.FindControl<TextBlock>("CurrentPathText")!.Text);
    }

    [AvaloniaFact]
    public async Task SearchState_ShowsSecondaryResultsAndCanBeCleared()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleHeadlessSearch");
        var match = Entry(Path.Combine(root, "match.txt"), ExplorerNodeKind.File);
        var provider = new ImmediateProvider(root, match);
        await session.OpenRootAsync(provider, provider);

        await session.SearchAsync("match");
        Assert.True(window.FindControl<Border>("SearchResultsPanel")!.IsVisible);
        Assert.Equal("1 MATCHES", window.FindControl<TextBlock>("SearchSummaryText")!.Text);

        session.ClearSearch();
        Assert.False(window.FindControl<Border>("SearchResultsPanel")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task GraphKeyboardNavigation_SelectsOpensAndReturns()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleHeadlessKeyboard");
        var child = Entry(Path.Combine(root, "child"), ExplorerNodeKind.Folder);
        var provider = new ImmediateProvider(root, child);
        await session.OpenRootAsync(provider, provider);
        var graph = window.FindControl<Control>("GraphScene")!;
        graph.Focus();

        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        Assert.Equal(child.Id, session.SelectedNode!.Id);

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.Equal(child.Path, session.CurrentPath);

        graph.Focus();
        window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        Assert.Equal(root, session.CurrentPath);

        window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
        Assert.True(window.FindControl<TextBox>("SearchBox")!.IsFocused);
    }

    [AvaloniaTheory]
    [InlineData(12, false, "small")]
    [InlineData(47, false, "medium")]
    [InlineData(180, false, "large-180")]
    [InlineData(5_000, false, "aggregate-heavy")]
    [InlineData(180, true, "search-highlight")]
    public async Task RepresentativeScene_IsBoundedAndReportsLocalDiagnostics(
        int itemCount,
        bool searchActive,
        string profileName)
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), $"OmniBrilleProfile-{profileName}");
        var provider = new DenseProvider(root, itemCount);
        await session.OpenRootAsync(provider, provider);
        if (searchActive)
        {
            await session.SearchAsync("item");
        }

        var graph = window.FindControl<GraphSceneControl>("GraphScene")!;
        graph.Focus();
        window.KeyPress(Key.D0, RawInputModifiers.None, PhysicalKey.Digit0, "0");
        var diagnostics = graph.Diagnostics;

        Assert.Equal(Math.Min(GraphNeighborhoodBuilder.DefaultNodeBudget, itemCount + 1), diagnostics.Nodes);
        Assert.Equal(diagnostics.Nodes - 1, diagnostics.Edges);
        Assert.InRange(diagnostics.Labels, 1, GraphPresentationPolicy.RecommendedLabelBudget(1, diagnostics.Nodes));
        Assert.True(diagnostics.Nodes <= session.SceneBudget);
        Assert.True(diagnostics.LastRenderDuration > TimeSpan.Zero);
        if (itemCount >= GraphNeighborhoodBuilder.DefaultNodeBudget)
        {
            Assert.Contains(session.Neighborhood!.Nodes, node => node.Kind == ExplorerNodeKind.Aggregate);
        }

        _output.WriteLine(
            $"{profileName}: source={itemCount}, nodes={diagnostics.Nodes}, edges={diagnostics.Edges}, " +
            $"labels={diagnostics.Labels}, layout={diagnostics.LayoutDuration.TotalMilliseconds:0.000} ms, " +
            $"prep={diagnostics.ScenePreparationDuration.TotalMilliseconds:0.000} ms, " +
            $"render={diagnostics.LastRenderDuration.TotalMilliseconds:0.000} ms, " +
            $"load={session.LastLoadDuration.TotalMilliseconds:0.000} ms");
    }

    private static MainWindow CreateWindow(out ExplorerSession session, out MemoryPreferencesStore store)
    {
        session = new ExplorerSession();
        store = new MemoryPreferencesStore();
        return new MainWindow(session, store);
    }

    private static ExplorerEntry Entry(string path, ExplorerNodeKind kind) =>
        new(path, Path.GetFileName(path), path, kind);

    private sealed class MemoryPreferencesStore : IVisualPreferencesStore
    {
        public VisualPreferences? Saved { get; private set; }

        public VisualPreferences Load() => Saved ?? new VisualPreferences();

        public void Save(VisualPreferences preferences) => Saved = preferences;
    }

    private sealed class DeferredProvider : IExplorerProvider, IExplorerSearchProvider
    {
        private readonly TaskCompletionSource<ExplorerDirectorySnapshot> _snapshot =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DeferredProvider(string root)
        {
            AccessRoot = root;
        }

        public string AccessRoot { get; }

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken) =>
            _snapshot.Task;

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult([], false, 0));

        public void Complete(ExplorerDirectorySnapshot snapshot) => _snapshot.TrySetResult(snapshot);
    }

    private sealed class ImmediateProvider : IExplorerProvider, IExplorerSearchProvider
    {
        private readonly ExplorerEntry _match;

        public ImmediateProvider(string root, ExplorerEntry match)
        {
            AccessRoot = root;
            _match = match;
        }

        public string AccessRoot { get; }

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerDirectorySnapshot(
                Entry(path, ExplorerNodeKind.Folder),
                StringComparer.OrdinalIgnoreCase.Equals(path, _match.Path) ? [] : [_match]));

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult(
                [new ExplorerSearchHit(_match.Id, _match.Name, _match.Path, _match.Kind)],
                false,
                1));
    }

    private sealed class DenseProvider : IExplorerProvider, IExplorerSearchProvider
    {
        private readonly ExplorerEntry[] _entries;

        public DenseProvider(string root, int itemCount)
        {
            AccessRoot = root;
            _entries = Enumerable.Range(0, itemCount)
                .Select(index => Entry(
                    Path.Combine(root, $"item-{index:D5}{(index % 5 == 0 ? string.Empty : ".txt")}"),
                    index % 5 == 0 ? ExplorerNodeKind.Folder : ExplorerNodeKind.File))
                .ToArray();
        }

        public string AccessRoot { get; }

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerDirectorySnapshot(Entry(path, ExplorerNodeKind.Folder), _entries));

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult(
                _entries.Take(8).Select(entry => new ExplorerSearchHit(entry.Id, entry.Name, entry.Path, entry.Kind)).ToArray(),
                true,
                1));
    }
}
