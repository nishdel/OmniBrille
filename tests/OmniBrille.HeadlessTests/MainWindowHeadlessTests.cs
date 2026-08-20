using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
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
using OmniBrille.Infrastructure.OmniSorSe;
using Protocol = OmniSorSe.ExplorerProtocol;

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
        Assert.Equal("Run Standalone Search", AutomationProperties.GetName(window.FindControl<Button>("SearchButton")!));
        Assert.Equal("Standalone Search results", AutomationProperties.GetName(window.FindControl<ListBox>("SearchResultsList")!));
        Assert.Equal("Theme", AutomationProperties.GetName(window.FindControl<ComboBox>("ThemePicker")!));
        Assert.True(window.FindControl<Border>("WelcomePanel")!.IsVisible);
        Assert.Equal("First-run guidance", AutomationProperties.GetName(window.FindControl<Border>("WelcomePanel")!));
        Assert.Contains("Context and Hybrid", window.FindControl<TextBlock>("WelcomeCompanionText")!.Text, StringComparison.Ordinal);
        Assert.Equal(
            "Choose a folder to explore in Standalone mode",
            AutomationProperties.GetName(window.FindControl<Button>("WelcomeChooseFolderButton")!));
        var graph = window.FindControl<Control>("GraphScene")!;
        Assert.Equal("Spatial Structure graph", AutomationProperties.GetName(graph));
        Assert.Equal("Spatial Structure graph", ControlAutomationPeer.CreatePeerForElement(graph).GetName());
    }

    [AvaloniaFact]
    public void MinimumWindow_UsesBoundedTwoRowHudWithoutHorizontalOverflow()
    {
        using var window = CreateWindow(out _, out _);
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        window.Show();

        var header = window.FindControl<Grid>("HeaderLayout")!;
        Assert.InRange(header.DesiredSize.Width, 1, window.ClientSize.Width - 31);
        Assert.True(window.FindControl<Button>("ChooseFolderButton")!.IsEffectivelyVisible);
        Assert.True(window.FindControl<Button>("AccessibleListButton")!.IsEffectivelyVisible);
        Assert.True(window.FindControl<Button>("SettingsButton")!.IsEffectivelyVisible);
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
    public async Task SanitizedDiagnosticsSurface_IsAccessibleAndExcludesSessionData()
    {
        var session = new ExplorerSession();
        using var window = new MainWindow(
            session,
            new MemoryPreferencesStore(),
            connection: new FakeConnectedCoordinator(),
            handoffEndpoint: "one-time-handoff");
        window.Show();
        await WaitUntilAsync(() => session.ProviderMode == ExplorerProviderMode.Connected && !session.IsLoading);
        await session.SearchAsync("private search query");

        var copyButton = window.FindControl<Button>("CopyDiagnosticsButton")!;
        Assert.Equal("Copy sanitized support diagnostics", AutomationProperties.GetName(copyButton));

        var report = window.CreateSanitizedDiagnosticsReport();
        Assert.Contains("OmniBrille safe diagnostics", report, StringComparison.Ordinal);
        Assert.Contains("1.0.0", report, StringComparison.Ordinal);
        Assert.Contains("Provider: Connected", report, StringComparison.Ordinal);
        Assert.DoesNotContain("one-time-handoff", report, StringComparison.Ordinal);
        Assert.DoesNotContain("private search query", report, StringComparison.Ordinal);
        Assert.DoesNotContain("opaque-", report, StringComparison.Ordinal);
        Assert.DoesNotContain("report.txt", report, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", report, StringComparison.OrdinalIgnoreCase);
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
        Assert.False(window.FindControl<Border>("DetailsPanel")!.IsVisible);
        Assert.False(window.FindControl<StackPanel>("SearchEmptyState")!.IsVisible);
        Assert.True(window.FindControl<Button>("FocusSearchResultButton")!.IsEnabled);

        session.ClearSearch();
        Assert.False(window.FindControl<Border>("SearchResultsPanel")!.IsVisible);
        Assert.True(window.FindControl<Border>("DetailsPanel")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task EmptySearch_ShowsExplanationAndDisablesUnavailableFocusAction()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleHeadlessEmptySearch");
        var provider = new DeferredProvider(root);
        var opening = session.OpenRootAsync(provider, provider);
        provider.Complete(new ExplorerDirectorySnapshot(Entry(root, ExplorerNodeKind.Folder), []));
        await opening;

        await session.SearchAsync("nothing-here");

        Assert.True(window.FindControl<Border>("SearchResultsPanel")!.IsVisible);
        Assert.True(window.FindControl<StackPanel>("SearchEmptyState")!.IsVisible);
        Assert.Equal("No Search matches", AutomationProperties.GetName(window.FindControl<StackPanel>("SearchEmptyState")!));
        Assert.False(window.FindControl<ListBox>("SearchResultsList")!.IsVisible);
        Assert.False(window.FindControl<Button>("FocusSearchResultButton")!.IsEnabled);
        Assert.False(window.FindControl<Border>("DetailsPanel")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task EmptyStructureFolder_ExplainsFocusAndOffersProviderAppropriateRecovery()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleHeadlessEmptyFolder");
        var provider = new DeferredProvider(root);
        var opening = session.OpenRootAsync(provider, provider);
        provider.Complete(new ExplorerDirectorySnapshot(Entry(root, ExplorerNodeKind.Folder), []));
        await opening;

        Assert.True(window.FindControl<Border>("StructureEmptyPanel")!.IsVisible);
        Assert.Equal("Empty Structure folder", AutomationProperties.GetName(window.FindControl<Border>("StructureEmptyPanel")!));
        Assert.Contains("focus remains visible", session.Status, StringComparison.Ordinal);
        Assert.True(window.FindControl<Button>("StructureEmptyChooseButton")!.IsVisible);
        Assert.False(window.FindControl<Button>("StructureEmptyProviderButton")!.IsVisible);
        Assert.False(window.FindControl<Button>("StructureEmptyBackButton")!.IsEnabled);
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

    [AvaloniaFact]
    public async Task GraphAutomationPeer_ExposesOnlyVisibleNodesWithStateAndAction()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleAutomationRoot");
        var child = Entry(Path.Combine(root, "child"), ExplorerNodeKind.Folder);
        var provider = new ImmediateProvider(root, child);
        await session.OpenRootAsync(provider, provider);
        var graph = window.FindControl<GraphSceneControl>("GraphScene")!;

        var peer = ControlAutomationPeer.CreatePeerForElement(graph);
        var nodePeers = peer.GetChildren();

        Assert.Equal(2, nodePeers.Count);
        Assert.All(nodePeers, nodePeer => Assert.Equal(AutomationControlType.TreeItem, nodePeer.GetAutomationControlType()));
        var focusPeer = Assert.Single(nodePeers, nodePeer => nodePeer.GetItemStatus()!.Contains("Current focus"));
        Assert.Contains(Path.GetFileName(root), focusPeer.GetName());
        var childPeer = Assert.Single(nodePeers, nodePeer => nodePeer.GetName().Contains("child"));
        childPeer.SetFocus();
        Assert.Equal(child.Id, session.SelectedNode!.Id);

        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(childPeer.GetProvider<IInvokeProvider>());
        invoke.Invoke();
        Assert.Equal(child.Path, session.CurrentPath);
    }

    [AvaloniaFact]
    public async Task AccessibleList_UsesSharedSelectionNavigationSearchAndBackState()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleAccessibleListRoot");
        var child = Entry(Path.Combine(root, "match-child"), ExplorerNodeKind.Folder);
        var provider = new ImmediateProvider(root, child);
        await session.OpenRootAsync(provider, provider);

        window.KeyPress(
            Key.L,
            RawInputModifiers.Control | RawInputModifiers.Shift,
            PhysicalKey.L,
            "l");
        var panel = window.FindControl<Border>("AccessibleListPanel")!;
        var list = window.FindControl<ListBox>("AccessibleNodesList")!;
        Assert.True(panel.IsVisible);
        Assert.Equal(2, list.ItemCount);
        var accessibleNames = list.ItemsSource!.Cast<object>().Select(item => item.ToString()).ToArray();
        Assert.Contains($"{Path.GetFileName(root)}, Folder, focus, selected", accessibleNames);
        Assert.Contains("match-child, Folder", accessibleNames);

        list.SelectedIndex = 1;
        Assert.Equal(child.Id, session.SelectedNode!.Id);
        await session.SearchAsync("match");
        var selected = list.SelectedItem;
        Assert.NotNull(selected);
        var state = selected.GetType().GetProperty("StateText")!.GetValue(selected) as string;
        Assert.Contains("MATCH", state);

        window.FindControl<Button>("AccessibleOpenButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(child.Path, session.CurrentPath);
        Assert.Equal(child.Path, window.FindControl<TextBlock>("AccessibleFocusText")!.Text);

        window.FindControl<Button>("AccessibleBackButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(root, session.CurrentPath);
        Assert.True(panel.IsVisible);
    }

    [AvaloniaFact]
    public async Task ConnectedMode_UsesSharedGraphListSearchDetailsAndAccessibleStatus()
    {
        var session = new ExplorerSession();
        var store = new MemoryPreferencesStore();
        var connection = new FakeConnectedCoordinator();
        using var window = new MainWindow(
            session,
            store,
            connection: connection,
            handoffEndpoint: "one-time-handoff");
        window.Show();
        await WaitUntilAsync(() => session.ProviderMode == ExplorerProviderMode.Connected && !session.IsLoading);

        var statusButton = window.FindControl<Button>("ConnectionButton")!;
        Assert.Equal("Connected · OmniSorSe", statusButton.Content);
        Assert.Equal(
            "Provider status: Connected · OmniSorSe",
            AutomationProperties.GetName(statusButton));
        Assert.Equal(
            "Structure Search through OmniSorSe",
            AutomationProperties.GetName(window.FindControl<TextBox>("SearchBox")!));
        Assert.Equal(
            "Run OmniSorSe Search",
            AutomationProperties.GetName(window.FindControl<Button>("SearchButton")!));
        Assert.Equal(
            "OmniSorSe Search results",
            AutomationProperties.GetName(window.FindControl<ListBox>("SearchResultsList")!));
        Assert.Equal("opaque-root", session.AccessRoot);
        Assert.Contains(session.Neighborhood!.Nodes, node => node.Id == "opaque-folder");

        window.FindControl<Button>("AccessibleListButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var list = window.FindControl<ListBox>("AccessibleNodesList")!;
        Assert.Equal(4, list.ItemCount);
        list.SelectedItem = list.ItemsSource!.Cast<object>()
            .Single(item => item.ToString()!.Contains("Indexed Folder", StringComparison.Ordinal));
        Assert.Equal("opaque-folder", session.SelectedNode!.Target);
        await WaitUntilAsync(() => session.SelectedNodeDetails?.Summary == "Indexed folder details");
        Assert.Equal("Indexed folder details", window.FindControl<TextBlock>("DetailsSummaryText")!.Text);

        await session.SearchAsync("report");
        Assert.Equal("opaque-file", Assert.Single(session.SearchResult!.Hits).Target);
        Assert.True(window.FindControl<Border>("SearchResultsPanel")!.IsVisible is false);
        Assert.Contains(list.ItemsSource!.Cast<object>(), item =>
            item.GetType().GetProperty("StateText")?.GetValue(item)?.ToString()?.Contains("MATCH", StringComparison.Ordinal) is true);

        window.FindControl<Button>("AccessibleOpenButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => session.CurrentPath.Contains("Indexed Folder", StringComparison.Ordinal));
        Assert.True(session.CanGoBack);
        window.FindControl<Button>("AccessibleBackButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => session.Neighborhood?.Focus.Id == "opaque-root");
    }

    [AvaloniaFact]
    public async Task ConnectedContext_UsesAuthoritativeRelationsAcrossGraphListDetailsAndModes()
    {
        var session = new ExplorerSession();
        var connection = new FakeConnectedCoordinator();
        using var window = new MainWindow(
            session,
            new MemoryPreferencesStore(),
            connection: connection,
            handoffEndpoint: "one-time-handoff");
        window.Show();
        await WaitUntilAsync(() => session.ProviderMode == ExplorerProviderMode.Connected && !session.IsLoading);

        session.SelectNode("opaque-file");
        window.FindControl<RadioButton>("ContextModeButton")!.IsChecked = true;
        await WaitUntilAsync(() => session.ViewMode == ExplorerViewMode.Context && !session.IsLoading);

        Assert.Equal("OmniBrille — Context", window.Title);
        Assert.Equal("CONTEXT", window.FindControl<TextBlock>("ViewModeStatusText")!.Text);
        Assert.True(window.FindControl<RadioButton>("ContextModeButton")!.IsChecked);
        var graph = window.FindControl<GraphSceneControl>("GraphScene")!;
        Assert.Equal("Spatial Context graph", AutomationProperties.GetName(graph));
        Assert.Equal(2, session.Neighborhood!.Nodes.Count);
        Assert.Single(session.Neighborhood.Edges, edge => edge.Kind == ExplorerGraphEdgeKind.Contextual);

        var peers = ControlAutomationPeer.CreatePeerForElement(graph).GetChildren();
        var relatedPeer = Assert.Single(peers, peer => peer.GetName().Contains("related.txt", StringComparison.Ordinal));
        Assert.Contains("Contextually related", relatedPeer.GetItemStatus());
        Assert.Contains("Shared indexed topic", relatedPeer.GetHelpText());

        window.FindControl<Button>("AccessibleListButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var list = window.FindControl<ListBox>("AccessibleNodesList")!;
        var relatedItem = Assert.Single(list.ItemsSource!.Cast<object>(), item =>
            item.ToString()!.Contains("related.txt", StringComparison.Ordinal));
        list.SelectedItem = relatedItem;
        await WaitUntilAsync(() => session.SelectedNode?.Id == "opaque-related");
        Assert.True(window.FindControl<StackPanel>("RelationshipDetailsSection")!.IsVisible);
        Assert.Contains("Shared indexed topic", window.FindControl<TextBlock>("DetailsRelationshipText")!.Text);
        var relationshipHelp = AutomationProperties.GetHelpText(
            window.FindControl<StackPanel>("RelationshipDetailsSection")!);
        Assert.NotNull(relationshipHelp);
        Assert.Contains("Shared indexed topic", relationshipHelp!, StringComparison.Ordinal);
        Assert.Contains("Evidence Derived", relationshipHelp, StringComparison.Ordinal);
        Assert.Contains("Source Content Intelligence", relationshipHelp, StringComparison.Ordinal);
        Assert.Equal(
            relationshipHelp,
            AutomationProperties.GetHelpText(window.FindControl<Border>("DetailsPanel")!));
        Assert.Contains("Content Intelligence 1", window.FindControl<TextBlock>("DetailsProvenanceText")!.Text);
        Assert.Contains(
            "Shared indexed topic",
            AutomationProperties.GetName(window.FindControl<TextBlock>("DetailsRelationshipText")!),
            StringComparison.Ordinal);
        Assert.Contains(
            "Derived",
            AutomationProperties.GetName(window.FindControl<TextBlock>("DetailsRelationshipEvidenceText")!),
            StringComparison.Ordinal);
        Assert.Contains(
            "Content Intelligence",
            AutomationProperties.GetName(window.FindControl<TextBlock>("DetailsProvenanceText")!),
            StringComparison.Ordinal);

        window.FindControl<Button>("AccessibleOpenButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => session.Neighborhood?.Focus.Id == "opaque-related");
        Assert.True(session.CanGoBack);

        window.FindControl<RadioButton>("StructureModeButton")!.IsChecked = true;
        await WaitUntilAsync(() => session.ViewMode == ExplorerViewMode.Structure);
        Assert.Equal("Spatial Structure graph", AutomationProperties.GetName(graph));
        Assert.Equal("STRUCTURE", window.FindControl<TextBlock>("ViewModeStatusText")!.Text);
    }

    [AvaloniaFact]
    public async Task ContextFilters_AreAccessibleReversibleAndKeepOneSharedGraphState()
    {
        var session = new ExplorerSession();
        using var window = new MainWindow(
            session,
            new MemoryPreferencesStore(),
            connection: new FakeConnectedCoordinator(),
            handoffEndpoint: "one-time-handoff");
        window.Show();
        await WaitUntilAsync(() => session.ProviderMode == ExplorerProviderMode.Connected && !session.IsLoading);
        session.SelectNode("opaque-file");
        window.FindControl<RadioButton>("ContextModeButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => session.ViewMode == ExplorerViewMode.Context && !session.IsLoading);

        var filterButton = window.FindControl<Button>("ContextFilterButton")!;
        Assert.True(filterButton.IsVisible);
        Assert.Contains("Context filters", AutomationProperties.GetName(filterButton), StringComparison.Ordinal);
        filterButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(window.FindControl<Border>("ContextFilterPanel")!.IsVisible);
        Assert.Equal("Context relationship filters", AutomationProperties.GetName(
            window.FindControl<Border>("ContextFilterPanel")!));

        var filterUpdate = System.Diagnostics.Stopwatch.StartNew();
        window.FindControl<ComboBox>("ContextStrengthFilter")!.SelectedIndex = 3;
        await WaitUntilAsync(() => session.ContextFilter.MinimumStrength == 100);
        filterUpdate.Stop();
        Assert.Single(session.Neighborhood!.Nodes);
        Assert.True(session.ContextFilter.IsActive);
        Assert.Contains("active", filterButton.Content!.ToString(), StringComparison.OrdinalIgnoreCase);

        filterButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(window.FindControl<Border>("ContextEmptyPanel")!.IsVisible);
        window.FindControl<Button>("ContextEmptyClearButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => !session.ContextFilter.IsActive);

        Assert.Equal(2, session.Neighborhood.Nodes.Count);
        Assert.False(window.FindControl<Border>("ContextEmptyPanel")!.IsVisible);
        _output.WriteLine($"context-filter-update={filterUpdate.Elapsed.TotalMilliseconds:0.000} ms");
    }

    [AvaloniaFact]
    public async Task ConnectedHybrid_CombinesRolesAcrossGraphListDetailsFiltersAndCtrl3()
    {
        var session = new ExplorerSession();
        var store = new MemoryPreferencesStore();
        store.Save(new VisualPreferences(ReducedMotion: true, ReducedEffects: true));
        using var window = new MainWindow(
            session,
            store,
            connection: new FakeConnectedCoordinator(includeHybridStructure: true),
            handoffEndpoint: "one-time-handoff");
        window.Show();
        await WaitUntilAsync(() => session.ProviderMode == ExplorerProviderMode.Connected && !session.IsLoading);
        session.SelectNode("opaque-file");

        window.KeyPress(Key.D3, RawInputModifiers.Control, PhysicalKey.Digit3, "3");
        await WaitUntilAsync(() => session.ViewMode == ExplorerViewMode.Hybrid && !session.IsLoading);

        Assert.Equal("OmniBrille — Hybrid", window.Title);
        Assert.Equal("HYBRID", window.FindControl<TextBlock>("ViewModeStatusText")!.Text);
        Assert.True(window.FindControl<RadioButton>("HybridModeButton")!.IsChecked);
        var graph = window.FindControl<GraphSceneControl>("GraphScene")!;
        Assert.Equal("Spatial Hybrid graph", AutomationProperties.GetName(graph));
        Assert.True(graph.ReducedMotion);
        Assert.True(graph.ReducedEffects);
        Assert.Contains(session.Neighborhood!.Edges, edge => edge.Kind == ExplorerGraphEdgeKind.Structural);
        Assert.Contains(session.Neighborhood.Edges, edge => edge.Kind == ExplorerGraphEdgeKind.Contextual);
        Assert.Equal(session.Neighborhood.Nodes.Count, session.Neighborhood.Nodes.Select(node => node.Id).Distinct().Count());

        var focus = session.Neighborhood.Focus;
        Assert.Equal(ExplorerNodeRole.Structural | ExplorerNodeRole.Contextual, focus.Roles);
        var peers = ControlAutomationPeer.CreatePeerForElement(graph).GetChildren();
        Assert.Contains(peers, peer => peer.GetName().Contains("structural and contextually related", StringComparison.OrdinalIgnoreCase));

        window.FindControl<Button>("AccessibleListButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var list = window.FindControl<ListBox>("AccessibleNodesList")!;
        var relatedItem = Assert.Single(list.ItemsSource!.Cast<object>(), item =>
            item.ToString()!.Contains("related.txt", StringComparison.Ordinal));
        Assert.Contains("Context", relatedItem.ToString(), StringComparison.Ordinal);
        list.SelectedItem = relatedItem;
        await WaitUntilAsync(() => session.SelectedNode?.Id == "opaque-related");
        Assert.True(window.FindControl<StackPanel>("RelationshipDetailsSection")!.IsVisible);
        Assert.Contains("Shared indexed topic", window.FindControl<TextBlock>("DetailsRelationshipText")!.Text);

        window.FindControl<Button>("AccessibleListButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var structuralCount = session.Neighborhood.Edges.Count(edge => edge.Kind == ExplorerGraphEdgeKind.Structural);
        window.FindControl<Button>("ContextFilterButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.FindControl<ComboBox>("ContextStrengthFilter")!.SelectedIndex = 3;
        await WaitUntilAsync(() => session.ContextFilter.MinimumStrength == 100);
        Assert.Equal(structuralCount, session.Neighborhood.Edges.Count(edge => edge.Kind == ExplorerGraphEdgeKind.Structural));
        Assert.DoesNotContain(session.Neighborhood.Edges, edge => edge.Kind == ExplorerGraphEdgeKind.Contextual);

        window.FindControl<ComboBox>("ThemePicker")!.SelectedIndex = 1;
        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public async Task StandaloneCtrl3_KeepsStructureAndExplainsHybridAuthority()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleHybridStandalone");
        var provider = new ImmediateProvider(root, Entry(Path.Combine(root, "item.txt"), ExplorerNodeKind.File));
        await session.OpenRootAsync(provider, provider);

        window.KeyPress(Key.D3, RawInputModifiers.Control, PhysicalKey.Digit3, "3");
        await WaitUntilAsync(() => session.Status.Contains("Hybrid exploration requires OmniSorSe", StringComparison.Ordinal));

        Assert.Equal(ExplorerViewMode.Structure, session.ViewMode);
        Assert.True(window.FindControl<RadioButton>("StructureModeButton")!.IsChecked);
        Assert.False(window.FindControl<Button>("ContextFilterButton")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task ConnectedDisconnect_IsAnnouncedAndStandaloneSwitchClearsOpaqueSession()
    {
        var session = new ExplorerSession();
        var connection = new FakeConnectedCoordinator();
        using var window = new MainWindow(
            session,
            new MemoryPreferencesStore(),
            connection: connection,
            handoffEndpoint: "one-time-handoff");
        window.Show();
        await WaitUntilAsync(() => session.ProviderMode == ExplorerProviderMode.Connected && !session.IsLoading);

        connection.ReportDisconnected(new IOException("controlled disconnect"));
        await WaitUntilAsync(() => Equals(window.FindControl<Button>("ConnectionButton")!.Content, "OmniSorSe disconnected"));
        Assert.Equal(
            "Provider status: OmniSorSe disconnected",
            AutomationProperties.GetName(window.FindControl<Button>("ConnectionButton")!));
        Assert.NotNull(session.Neighborhood);

        window.FindControl<Button>("ConnectionButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.FindControl<Button>("UseStandaloneButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(ExplorerProviderMode.Standalone, session.ProviderMode);
        Assert.Null(session.Neighborhood);
        Assert.Equal("Standalone", window.FindControl<Button>("ConnectionButton")!.Content);
    }

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public async Task RepresentativeTextScale_KeepsHudListAndGraphUsable(double scale)
    {
        using var window = CreateWindow(out var session, out _);
        window.FontSize = 13 * scale;
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), $"OmniBrilleScale-{scale:0.00}");
        var provider = new DenseProvider(root, 47);
        await session.OpenRootAsync(provider, provider);
        var graph = window.FindControl<GraphSceneControl>("GraphScene")!;
        graph.TextScale = scale;
        graph.ResetView();
        window.FindControl<Button>("AccessibleListButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        using (window.CaptureRenderedFrame())
        {
        }

        Assert.True(window.FindControl<Button>("ChooseFolderButton")!.Bounds.Height > 0);
        Assert.True(window.FindControl<TextBox>("SearchBox")!.Bounds.Height > 0);
        Assert.True(window.FindControl<Border>("AccessibleListPanel")!.IsVisible);
        Assert.True(window.FindControl<ListBox>("AccessibleNodesList")!.Bounds.Height > 0);
        Assert.True(graph.Diagnostics.Labels <= GraphPresentationPolicy.RecommendedLabelBudget(1, 48, scale));
        Assert.Equal(scale, graph.Diagnostics.TextScale, 3);
    }

    [AvaloniaFact]
    public async Task ReducedMotion_StopsSceneAnimationAndSimplifiesDataRain()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        window.FindControl<Button>("SettingsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.FindControl<CheckBox>("ReducedMotionToggle")!.IsChecked = true;
        window.FindControl<CheckBox>("ReducedEffectsToggle")!.IsChecked = true;
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleReducedMotion");
        var provider = new DeferredProvider(root);

        var opening = session.OpenRootAsync(provider, provider);
        using (window.CaptureRenderedFrame())
        {
        }

        var graph = window.FindControl<GraphSceneControl>("GraphScene")!;
        var rain = window.FindControl<DataRainControl>("DataRain")!;
        Assert.False(graph.Diagnostics.AnimationActive);
        Assert.True(rain.Diagnostics.IsActive);
        Assert.InRange(rain.Diagnostics.RenderedTokens, 1, 6);

        provider.Complete(new ExplorerDirectorySnapshot(Entry(root, ExplorerNodeKind.Folder), []));
        await opening;
        Assert.False(rain.Diagnostics.IsActive);
    }

    [AvaloniaFact]
    public async Task SearchHighlight_ReducedEffectsLowersDecorativeRenderCost()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleReducedEffectsProfile");
        var provider = new DenseProvider(root, 180);
        await session.OpenRootAsync(provider, provider);
        await session.SearchAsync("item");
        var graph = window.FindControl<GraphSceneControl>("GraphScene")!;
        graph.ReducedMotion = true;
        graph.ReducedEffects = false;
        graph.SetScene(session.Neighborhood, session.SelectedNode?.Id, session.HighlightedNodeIds, animate: false);
        using (window.CaptureRenderedFrame())
        {
        }

        var full = graph.Diagnostics;
        graph.ReducedEffects = true;
        graph.InvalidateVisual();
        using (window.CaptureRenderedFrame())
        {
        }

        var reduced = graph.Diagnostics;
        Assert.Equal(full.Nodes, reduced.Nodes);
        Assert.Equal(full.Labels, reduced.Labels);
        Assert.True(reduced.EdgeDuration < full.EdgeDuration);
        _output.WriteLine(
            $"search-effects: full={full.LastRenderDuration.TotalMilliseconds:0.000} ms, " +
            $"reduced={reduced.LastRenderDuration.TotalMilliseconds:0.000} ms, " +
            $"full-edge={full.EdgeDuration.TotalMilliseconds:0.000} ms, " +
            $"reduced-edge={reduced.EdgeDuration.TotalMilliseconds:0.000} ms");
    }

    [AvaloniaFact]
    public async Task SyntheticContextDensity_RemainsInsideDocumentedCombinedBudget()
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleContextDensity");
        var provider = new DenseProvider(root, 180);
        await session.OpenRootAsync(provider, provider);
        var neighborhood = session.Neighborhood!;
        var ids = neighborhood.Nodes.Select(node => node.Id).ToArray();
        var candidates = Enumerable.Range(0, 500).Select(index => new ContextRelationshipCandidate(
            $"context-{index:D3}",
            ids[index % ids.Length],
            ids[(index + 7 + (index / ids.Length)) % ids.Length],
            1 - (index / 500d),
            TouchesFocus: index % 11 == 0));
        var relationships = ContextRenderBudgetPolicy.SelectRelationships(candidates, neighborhood.Edges.Count);
        var synthetic = neighborhood with
        {
            Edges = neighborhood.Edges
                .Concat(relationships.Select(item => new ExplorerEdge(
                    item.SourceId,
                    item.TargetId,
                    ExplorerGraphEdgeKind.Contextual,
                    new ExplorerRelationship(
                        item.Id,
                        item.SourceId,
                        item.TargetId,
                        ExplorerRelationshipKind.Related,
                        (int)Math.Round(item.Importance * 100),
                        "Synthetic renderer-pressure fixture",
                        ExplorerRelationshipEvidenceClass.Deterministic,
                        "Test fixture"))))
                .ToArray(),
            ViewMode = ExplorerViewMode.Context,
        };
        var graph = window.FindControl<GraphSceneControl>("GraphScene")!;
        graph.ReducedMotion = true;
        graph.ReducedEffects = false;
        graph.SetScene(synthetic, session.SelectedNode?.Id, new HashSet<string>(), animate: false);
        using (window.CaptureRenderedFrame())
        {
        }
        graph.InvalidateVisual();
        using (window.CaptureRenderedFrame())
        {
        }

        var fullEffects = graph.Diagnostics;
        graph.ReducedEffects = true;
        graph.InvalidateVisual();
        using (window.CaptureRenderedFrame())
        {
        }

        var reducedEffects = graph.Diagnostics;
        Assert.Equal(48, fullEffects.Nodes);
        Assert.True(fullEffects.Edges <= ContextRenderBudgetPolicy.Default.MaximumCombinedEdges);
        Assert.True(relationships.Count <= ContextRenderBudgetPolicy.Default.MaximumContextualEdges);
        _output.WriteLine(
            $"context-density: nodes={fullEffects.Nodes}, structural={neighborhood.Edges.Count}, " +
            $"contextual={relationships.Count}, combined={fullEffects.Edges}, " +
            $"full={fullEffects.LastRenderDuration.TotalMilliseconds:0.000} ms, " +
            $"reduced={reducedEffects.LastRenderDuration.TotalMilliseconds:0.000} ms, " +
            $"full-edges={fullEffects.EdgeDuration.TotalMilliseconds:0.000} ms, " +
            $"reduced-edges={reducedEffects.EdgeDuration.TotalMilliseconds:0.000} ms, " +
            $"label-prep={fullEffects.LabelPreparationDuration.TotalMilliseconds:0.000} ms, " +
            $"label-draw={fullEffects.LabelDrawDuration.TotalMilliseconds:0.000} ms, " +
            $"alloc={fullEffects.RenderAllocatedBytes:N0} B");
    }

    [AvaloniaTheory]
    [InlineData("small-hybrid", 8)]
    [InlineData("representative-hybrid", 24)]
    [InlineData("max-hybrid", 48)]
    public async Task HybridRenderer_ProfilesBoundedFullReducedAndSearchScenes(string profileName, int nodeCount)
    {
        using var window = CreateWindow(out var session, out _);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), $"OmniBrille-{profileName}");
        var provider = new DenseProvider(root, nodeCount - 1);
        await session.OpenRootAsync(provider, provider);
        var structural = session.Neighborhood!;
        var ids = structural.Nodes.Select(node => node.Id).ToArray();
        var candidates = Enumerable.Range(0, Math.Min(96, nodeCount * 3)).Select(index => new ContextRelationshipCandidate(
            $"hybrid-{index:D3}",
            ids[index % ids.Length],
            ids[(index + 3 + (index / ids.Length)) % ids.Length],
            1 - (index / 100d),
            TouchesFocus: index % 13 == 0));
        var relationships = ContextRenderBudgetPolicy.SelectRelationships(candidates, structural.Edges.Count);
        var contextualIds = relationships
            .SelectMany(item => new[] { item.SourceId, item.TargetId })
            .ToHashSet(ExplorerIdentity.Comparer);
        var hybrid = structural with
        {
            Nodes = structural.Nodes.Select(node => node with
            {
                Roles = ExplorerNodeRole.Structural |
                    (contextualIds.Contains(node.Id) ? ExplorerNodeRole.Contextual : ExplorerNodeRole.None),
            }).ToArray(),
            Edges = structural.Edges.Concat(relationships.Select(item => new ExplorerEdge(
                item.SourceId,
                item.TargetId,
                ExplorerGraphEdgeKind.Contextual,
                new ExplorerRelationship(
                    item.Id,
                    item.SourceId,
                    item.TargetId,
                    ExplorerRelationshipKind.Related,
                    (int)Math.Round(item.Importance * 100),
                    "Synthetic Hybrid renderer-pressure fixture",
                    ExplorerRelationshipEvidenceClass.Deterministic,
                    "Test fixture")))).ToArray(),
            ViewMode = ExplorerViewMode.Hybrid,
        };
        var graph = window.FindControl<GraphSceneControl>("GraphScene")!;
        SceneDiagnostics WarmAndSampleMedian()
        {
            using (window.CaptureRenderedFrame())
            {
            }

            var samples = new List<SceneDiagnostics>(5);
            for (var index = 0; index < 5; index++)
            {
                graph.InvalidateVisual();
                using (window.CaptureRenderedFrame())
                {
                }

                samples.Add(graph.Diagnostics);
            }

            return samples.OrderBy(sample => sample.LastRenderDuration).ElementAt(samples.Count / 2);
        }

        graph.ReducedMotion = true;
        graph.ReducedEffects = false;
        graph.SearchActive = false;
        graph.SetScene(hybrid, hybrid.FocusNodeId, new HashSet<string>(), animate: false);
        var full = WarmAndSampleMedian();
        graph.ReducedEffects = true;
        var reduced = WarmAndSampleMedian();
        graph.ReducedEffects = false;
        graph.SearchActive = true;
        graph.SetScene(
            hybrid,
            hybrid.FocusNodeId,
            hybrid.Nodes.Skip(1).Take(14).Select(node => node.Id).ToHashSet(ExplorerIdentity.Comparer),
            animate: false);
        var search = WarmAndSampleMedian();
        Assert.Equal(nodeCount, full.Nodes);
        Assert.True(full.Edges <= ContextRenderBudgetPolicy.Default.MaximumCombinedEdges);
        Assert.True(relationships.Count <= ContextRenderBudgetPolicy.Default.MaximumContextualEdges);
        Assert.True(full.LastRenderDuration > TimeSpan.Zero);
        _output.WriteLine(
            $"{profileName}: nodes={full.Nodes}, structural={structural.Edges.Count}, contextual={relationships.Count}, " +
            $"layout={full.LayoutDuration.TotalMilliseconds:0.000} ms, prep={full.ScenePreparationDuration.TotalMilliseconds:0.000} ms, " +
            $"full={full.LastRenderDuration.TotalMilliseconds:0.000} ms, reduced={reduced.LastRenderDuration.TotalMilliseconds:0.000} ms, " +
            $"search={search.LastRenderDuration.TotalMilliseconds:0.000} ms, edges={full.EdgeDuration.TotalMilliseconds:0.000} ms, " +
            $"labels={full.Labels}, alloc={full.RenderAllocatedBytes:N0} B");
    }

    [AvaloniaTheory]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    public async Task CandidateSceneBudget_ProfilesRemainBounded(int nodeBudget)
    {
        var session = new ExplorerSession(new GraphNeighborhoodBuilder(nodeBudget));
        var store = new MemoryPreferencesStore();
        using var window = new MainWindow(session, store);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), $"OmniBrilleBudget-{nodeBudget}");
        var provider = new DenseProvider(root, 180);
        await session.OpenRootAsync(provider, provider);
        var graph = window.FindControl<GraphSceneControl>("GraphScene")!;
        graph.ReducedMotion = true;
        graph.SetScene(session.Neighborhood, session.SelectedNode?.Id, new HashSet<string>(), animate: false);
        using (window.CaptureRenderedFrame())
        {
        }

        Assert.Equal(nodeBudget, graph.Diagnostics.Nodes);
        Assert.Equal(nodeBudget - 1, graph.Diagnostics.Edges);
        _output.WriteLine(
            $"budget-{nodeBudget}: render={graph.Diagnostics.LastRenderDuration.TotalMilliseconds:0.000} ms, " +
            $"labels={graph.Diagnostics.Labels}, alloc={graph.Diagnostics.RenderAllocatedBytes:N0} B");
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
            $"background={diagnostics.BackgroundDuration.TotalMilliseconds:0.000} ms, " +
            $"edges={diagnostics.EdgeDuration.TotalMilliseconds:0.000} ms, " +
            $"glyphs={diagnostics.GlyphDuration.TotalMilliseconds:0.000} ms, " +
            $"label-prep={diagnostics.LabelPreparationDuration.TotalMilliseconds:0.000} ms, " +
            $"collision={diagnostics.LabelCollisionDuration.TotalMilliseconds:0.000} ms, " +
            $"label-draw={diagnostics.LabelDrawDuration.TotalMilliseconds:0.000} ms, " +
            $"alloc={diagnostics.RenderAllocatedBytes:N0} B, " +
            $"load={session.LastLoadDuration.TotalMilliseconds:0.000} ms");
    }

    [AvaloniaFact]
    public async Task VoiceHud_IsKeyboardAccessibleAndExecutesDeterministicCommand()
    {
        var session = new ExplorerSession();
        var store = new MemoryPreferencesStore();
        store.Save(new VisualPreferences(VoiceEnabled: true));
        using var capture = new FakeVoiceCapture();
        using var speech = new FakeSpeechRecognition("use light mode");
        using var window = new MainWindow(
            session,
            store,
            audioCapture: capture,
            speechRecognition: speech);
        window.Show();

        var voiceButton = window.FindControl<Button>("VoiceButton")!;
        Assert.Contains("Push to talk", AutomationProperties.GetName(voiceButton), StringComparison.Ordinal);
        Assert.Equal("Enable local push-to-talk voice", AutomationProperties.GetName(
            window.FindControl<CheckBox>("VoiceEnabledToggle")!));

        window.KeyPress(
            Key.Space,
            RawInputModifiers.Control | RawInputModifiers.Shift,
            PhysicalKey.Space,
            " ");
        await WaitUntilAsync(() => window.Voice.State == VoiceCapabilityState.Listening);
        Assert.Contains("Listening", window.FindControl<TextBlock>("VoiceStateText")!.Text);

        voiceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => window.Voice.State == VoiceCapabilityState.Ready);

        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
        Assert.Equal("“use light mode”", window.FindControl<TextBlock>("VoiceTranscriptText")!.Text);
        Assert.False(window.FindControl<Button>("VoiceCancelButton")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task VoiceSearch_UsesExistingSessionSearchAndShowsTranscript()
    {
        var session = new ExplorerSession();
        var store = new MemoryPreferencesStore();
        store.Save(new VisualPreferences(VoiceEnabled: true, ReducedMotion: true, ReducedEffects: true));
        using var capture = new FakeVoiceCapture();
        using var speech = new FakeSpeechRecognition("find match");
        using var window = new MainWindow(
            session,
            store,
            audioCapture: capture,
            speechRecognition: speech);
        window.Show();
        var root = Path.Combine(Path.GetTempPath(), "OmniBrilleVoiceSearch");
        var match = Entry(Path.Combine(root, "match.txt"), ExplorerNodeKind.File);
        var provider = new ImmediateProvider(root, match);
        await session.OpenRootAsync(provider, provider);

        var voiceButton = window.FindControl<Button>("VoiceButton")!;
        voiceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => window.Voice.State == VoiceCapabilityState.Listening);
        Assert.Equal(1, window.FindControl<Border>("VoiceListeningRing")!.Opacity);
        voiceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => window.Voice.State == VoiceCapabilityState.Ready);

        Assert.Equal("match", session.SearchQuery);
        Assert.Single(session.SearchResult!.Hits);
        Assert.Equal("match", window.FindControl<TextBox>("SearchBox")!.Text);
        Assert.Equal(1, window.Voice.Diagnostics.TranscriptLength > 0 ? 1 : 0);
    }

    [AvaloniaFact]
    public async Task VoiceUnavailableState_DoesNotRequireRuntimeModelOrMicrophoneAtStartup()
    {
        var session = new ExplorerSession();
        var store = new MemoryPreferencesStore();
        store.Save(new VisualPreferences(VoiceEnabled: true));
        using var capture = new FakeVoiceCapture();
        using var speech = new FakeSpeechRecognition("ignored")
        {
            CapabilityState = VoiceCapabilityState.RuntimeMissing,
        };
        using var window = new MainWindow(
            session,
            store,
            audioCapture: capture,
            speechRecognition: speech);
        window.Show();

        window.FindControl<Button>("VoiceButton")!
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => window.Voice.State == VoiceCapabilityState.RuntimeMissing);

        Assert.Contains("RuntimeMissing", AutomationProperties.GetName(
            window.FindControl<Button>("VoiceButton")!), StringComparison.Ordinal);
        Assert.Equal(0, capture.StartCount);
        Assert.Equal("OmniBrille — Structure", window.Title);
    }

    [AvaloniaFact]
    public async Task ConnectedVoiceSearch_RoutesThroughExistingOmniSorSeProtocolClient()
    {
        var session = new ExplorerSession();
        var store = new MemoryPreferencesStore();
        store.Save(new VisualPreferences(VoiceEnabled: true));
        var connection = new FakeConnectedCoordinator();
        using var capture = new FakeVoiceCapture();
        using var speech = new FakeSpeechRecognition("show me report");
        using var window = new MainWindow(
            session,
            store,
            connection: connection,
            handoffEndpoint: "one-time-handoff",
            audioCapture: capture,
            speechRecognition: speech);
        window.Show();
        await WaitUntilAsync(() => session.ProviderMode == ExplorerProviderMode.Connected && !session.IsLoading);

        var voiceButton = window.FindControl<Button>("VoiceButton")!;
        voiceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => window.Voice.State == VoiceCapabilityState.Listening);
        voiceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => window.Voice.State == VoiceCapabilityState.Ready);

        Assert.Equal("report", connection.LastSearchQuery);
        Assert.Equal("opaque-file", Assert.Single(session.SearchResult!.Hits).Target);
        Assert.Contains("OmniSorSe Search", window.Voice.Status, StringComparison.Ordinal);
    }

    private static MainWindow CreateWindow(out ExplorerSession session, out MemoryPreferencesStore store)
    {
        session = new ExplorerSession();
        store = new MemoryPreferencesStore();
        return new MainWindow(session, store);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "The expected connected UI state was not reached before the test deadline.");
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

    private sealed class FakeVoiceCapture : IAudioCaptureService
    {
        public event Action<double>? LevelChanged;

        public int StartCount { get; private set; }

        public VoiceCapability GetCapability() => new(
            VoiceCapabilityState.Ready,
            "Microphone ready",
            "Fake microphone");

        public Task StartAsync(VoiceRecognitionOptions options, CancellationToken cancellationToken)
        {
            StartCount++;
            LevelChanged?.Invoke(0.7);
            return Task.CompletedTask;
        }

        public Task<VoiceAudioClip> StopAsync(CancellationToken cancellationToken) => Task.FromResult(
            new VoiceAudioClip(new byte[32_000], 16_000, TimeSpan.FromSeconds(1)));

        public Task CancelAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeSpeechRecognition(string transcript) : ISpeechRecognitionProvider
    {
        public VoiceCapabilityState CapabilityState { get; init; } = VoiceCapabilityState.Ready;

        public Task<VoiceCapability> GetCapabilityAsync(
            VoiceRecognitionOptions options,
            CancellationToken cancellationToken) => Task.FromResult(new VoiceCapability(
                CapabilityState,
                CapabilityState == VoiceCapabilityState.Ready ? "Voice ready" : "Runtime missing",
                "Fake speech",
                "Configured"));

        public Task<SpeechRecognitionResult> TranscribeAsync(
            VoiceAudioClip clip,
            VoiceRecognitionOptions options,
            CancellationToken cancellationToken) => Task.FromResult(new SpeechRecognitionResult(
                transcript,
                null,
                TimeSpan.FromMilliseconds(1),
                "Fake speech"));

        public void Dispose()
        {
        }
    }

    private sealed class FakeConnectedCoordinator : IOmniSorSeConnectionCoordinator
    {
        private readonly FakeConnectedClient _client;

        public FakeConnectedCoordinator(bool includeHybridStructure = false)
        {
            _client = new FakeConnectedClient(includeHybridStructure);
        }

        public event EventHandler? StateChanged;

        public OmniSorSeConnectionState State { get; private set; } = OmniSorSeConnectionState.Standalone;

        public string UserStatus => State switch
        {
            OmniSorSeConnectionState.Connected => "Connected · OmniSorSe",
            OmniSorSeConnectionState.Disconnected => "OmniSorSe disconnected",
            _ => "Standalone",
        };

        public Protocol.ExplorerProtocolInfo? ProtocolInfo => _client.Info;

        public IReadOnlyList<Protocol.ExplorerNode> AccessibleRoots => [_client.Root];

        public IExplorerProtocolClient? Client => _client;

        public string? LastSearchQuery => _client.LastSearchQuery;

        public OmniSorSeConnectionDiagnostics Diagnostics => _client.Diagnostics with { State = State };

        public Task<bool> ConnectFromHandoffAsync(string handoffEndpoint, CancellationToken cancellationToken = default)
        {
            State = OmniSorSeConnectionState.Connected;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(true);
        }

        public Task<bool> ConnectAsync(OmniSorSeSessionGrant grant, CancellationToken cancellationToken = default) =>
            ConnectFromHandoffAsync(grant.Endpoint, cancellationToken);

        public Task<bool> RetryAsync(CancellationToken cancellationToken = default) =>
            ConnectFromHandoffAsync("retry", cancellationToken);

        public void UseStandalone()
        {
            State = OmniSorSeConnectionState.Standalone;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ReportDisconnected(Exception exception)
        {
            State = OmniSorSeConnectionState.Disconnected;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeConnectedClient : IExplorerProtocolClient
    {
        private static readonly Protocol.ExplorerProtocolLimits Limits = new(
            65_536, 1_048_576, 500, 256, 512, 100, 100, 2, 320, 32, 32, 256, 4, 15);
        private readonly bool _includeHybridStructure;

        public FakeConnectedClient(bool includeHybridStructure = false)
        {
            _includeHybridStructure = includeHybridStructure;
        }

        public Protocol.ExplorerNode Root { get; } = Node(
            "opaque-root", "Authorized Root", Protocol.ExplorerNodeKind.Source, null, 3);

        private Protocol.ExplorerNode Folder { get; } = Node(
            "opaque-folder", "Indexed Folder", Protocol.ExplorerNodeKind.Folder, "opaque-root", 0);

        private Protocol.ExplorerNode File { get; } = Node(
            "opaque-file", "report.txt", Protocol.ExplorerNodeKind.File, "opaque-root", 0);

        private Protocol.ExplorerNode RelatedFile { get; } = Node(
            "opaque-related", "related.txt", Protocol.ExplorerNodeKind.File, "opaque-root", 0);

        public OmniSorSeSessionGrant Grant { get; } = new(
            "named-pipe", "ose-0123456789abcdef0123456789abcdef", "session", "secret",
            DateTimeOffset.UtcNow.AddMinutes(2), 1, 0);

        public OmniSorSeConnectionDiagnostics Diagnostics { get; } = new(
            OmniSorSeConnectionState.Connected, "named-pipe", "1.0", TimeSpan.Zero,
            0, 0, 0, 0, 0, null);

        public string? LastSearchQuery { get; private set; }

        public Protocol.ExplorerProtocolInfo Info { get; } = new(
            1, 0, "OmniSorSe", "2.4.0",
            Protocol.ExplorerCapability.Structure |
            Protocol.ExplorerCapability.Search |
            Protocol.ExplorerCapability.Context |
            Protocol.ExplorerCapability.RelatedFiles,
            Limits, true, "Local named pipe");

        public Task<Protocol.ExplorerProtocolInfo> GetProtocolInfoAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Info);

        public Task<Protocol.ExplorerNodePage> GetAccessibleRootsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new Protocol.ExplorerNodePage([Root], 1, false, null));

        public Task<Protocol.ExplorerNodePage> GetChildrenAsync(
            Protocol.ExplorerChildrenRequest request,
            CancellationToken cancellationToken) => Task.FromResult(
                request.ParentNodeId == Root.Id
                    ? new Protocol.ExplorerNodePage([Folder, File, RelatedFile], 3, false, null)
                    : new Protocol.ExplorerNodePage([], 0, false, null));

        public Task<Protocol.ExplorerNeighborhood> GetNeighborhoodAsync(
            Protocol.ExplorerNeighborhoodRequest request,
            CancellationToken cancellationToken)
        {
            var focus = request.NodeId == RelatedFile.Id ? RelatedFile : File;
            var related = request.NodeId == RelatedFile.Id ? File : RelatedFile;
            return Task.FromResult(new Protocol.ExplorerNeighborhood(
                focus.Id,
                _includeHybridStructure ? [focus, related, Root] : [focus, related],
                _includeHybridStructure
                    ? [Contains(Root.Id, focus.Id), Relationship(focus.Id, related.Id)]
                    : [Relationship(focus.Id, related.Id)],
                false,
                null));
        }

        public Task<Protocol.ExplorerRelatedResult> GetRelatedAsync(
            Protocol.ExplorerRelatedRequest request,
            CancellationToken cancellationToken)
        {
            var related = request.NodeId == RelatedFile.Id ? File : RelatedFile;
            return Task.FromResult(new Protocol.ExplorerRelatedResult(
                [related],
                [Relationship(request.NodeId, related.Id)],
                false));
        }

        public Task<Protocol.ExplorerSearchResult> SearchAsync(
            Protocol.ExplorerSearchRequest request,
            CancellationToken cancellationToken)
        {
            LastSearchQuery = request.Query;
            return Task.FromResult(new Protocol.ExplorerSearchResult(
                [new Protocol.ExplorerSearchHit(File, 1, 1, "Indexed name match", null, "Name")],
                false,
                "Authorized indexed scope",
                false));
        }

        public Task<Protocol.ExplorerNodeDetails> GetNodeDetailsAsync(
            Protocol.ExplorerNodeDetailsRequest request,
            CancellationToken cancellationToken)
        {
            var node = request.NodeId == Folder.Id
                ? Folder
                : request.NodeId == File.Id
                    ? File
                    : request.NodeId == RelatedFile.Id ? RelatedFile : Root;
            return Task.FromResult(new Protocol.ExplorerNodeDetails(
                node, null, null,
                node.Id == Folder.Id ? "Indexed folder details" : "Indexed node details",
                [], [], null, [], true));
        }

        public void ReportStaleResponseRejected()
        {
        }

        private static Protocol.ExplorerEdge Relationship(string sourceId, string targetId) => new(
            sourceId,
            targetId,
            Protocol.ExplorerEdgeKind.Topic,
            80,
            "Shared indexed topic",
            Protocol.ExplorerEvidenceClass.Derived,
            "Content Intelligence 1");

        private static Protocol.ExplorerEdge Contains(string sourceId, string targetId) => new(
            sourceId,
            targetId,
            Protocol.ExplorerEdgeKind.Contains,
            100,
            "Filesystem containment",
            Protocol.ExplorerEvidenceClass.Structural,
            "OmniSorSe Structure");

        private static Protocol.ExplorerNode Node(
            string id,
            string name,
            Protocol.ExplorerNodeKind kind,
            string? parentId,
            int childCount) => new(
                id, name, kind, parentId, null, null, null,
                new Dictionary<string, string>(), childCount, 0);
    }
}
