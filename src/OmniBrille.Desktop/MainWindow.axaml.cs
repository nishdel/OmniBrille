using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmniBrille.Core;
using OmniBrille.Desktop.Presentation;
using OmniBrille.Desktop.Support;
using OmniBrille.Infrastructure;
using OmniBrille.Infrastructure.OmniSorSe;
using OmniBrille.Infrastructure.Voice;
using Protocol = OmniSorSe.ExplorerProtocol;

namespace OmniBrille.Desktop;

public sealed partial class MainWindow : Window, IDisposable, IVoiceActionTarget
{
    private readonly ExplorerSession _session;
    private readonly IVisualPreferencesStore _preferencesStore;
    private readonly IOmniSorSeConnectionCoordinator _connection;
    private readonly string? _handoffEndpoint;
    private readonly DispatcherTimer _diagnosticsTimer;
    private readonly DispatcherTimer _voiceVisualTimer;
    private readonly DispatcherTimer _voiceTranscriptTimer;
    private readonly DispatcherTimer _detailsTypingTimer;
    private readonly VoiceInteractionCoordinator _voice;
    private readonly IFileActivationService _fileActivation;
    private readonly IInteractionSoundService _sound;
    private VisualPreferences _preferences;
    private ExplorerNeighborhood? _lastRenderedNeighborhood;
    private bool _detailsDismissed;
    private bool _isApplyingPreferences;
    private bool _isSwitchingViewMode;
    private bool _isApplyingContextFilters;
    private bool _isSynchronizingAccessibleList;
    private bool _voicePulseHigh;
    private string _detailsTypingTarget = string.Empty;
    private int _detailsTypingIndex;
    private string? _lastDetailsNodeId;
    private string? _lastAnnouncement;
    private bool _isDisposed;

    public MainWindow()
        : this(new ExplorerSession(), new JsonVisualPreferencesStore(), null, null, null, null)
    {
    }

    public MainWindow(string? startupRoot, string? startupTheme, string? handoffEndpoint = null)
        : this(
            new ExplorerSession(),
            new JsonVisualPreferencesStore(),
            startupRoot,
            startupTheme,
            null,
            handoffEndpoint)
    {
    }

    public MainWindow(
        ExplorerSession session,
        IVisualPreferencesStore preferencesStore,
        string? startupRoot = null,
        string? startupTheme = null,
        IOmniSorSeConnectionCoordinator? connection = null,
        string? handoffEndpoint = null,
        IAudioCaptureService? audioCapture = null,
        ISpeechRecognitionProvider? speechRecognition = null,
        IFileActivationService? fileActivation = null,
        IInteractionSoundService? interactionSound = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
        _connection = connection ?? new OmniSorSeConnectionCoordinator();
        _handoffEndpoint = handoffEndpoint;
        _fileActivation = fileActivation ?? new ShellFileActivationService();
        _sound = interactionSound ?? new CyberInteractionSoundService();
        _preferences = _preferencesStore.Load().Normalize();
        _sound.Enabled = _preferences.SoundEnabled;
        _voice = new VoiceInteractionCoordinator(
            audioCapture ?? new WindowsWaveInAudioCaptureService(),
            speechRecognition ?? new WhisperCliSpeechRecognitionProvider(),
            this);
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
        _session.ProviderFailed += OnProviderFailed;
        _connection.StateChanged += OnConnectionStateChanged;
        _voice.StateChanged += OnVoiceStateChanged;
        AccessibleNodesList.AddHandler(
            InputElement.KeyDownEvent,
            OnAccessibleNodesKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        GraphScene.NodeSelected += OnGraphNodeSelected;
        GraphScene.NodeActivated += OnGraphNodeActivated;
        GraphScene.NodeHovered += OnGraphNodeHovered;
        GraphScene.BackRequested += async (_, _) => await GoBackAsync();
        GraphScene.DismissRequested += (_, _) => DismissTransientSurfaces();
        KeyDown += OnWindowKeyDown;
        Activated += (_, _) => UpdateMotionActivity();
        Deactivated += (_, _) => UpdateMotionActivity();
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty)
            {
                UpdateMotionActivity();
            }
        };
        Closed += (_, _) => Dispose();

        _diagnosticsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _diagnosticsTimer.Tick += (_, _) => UpdateDiagnostics();
        if (_preferences.DiagnosticsVisible)
        {
            _diagnosticsTimer.Start();
        }

        _voiceVisualTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _voiceVisualTimer.Tick += (_, _) => UpdateVoiceListeningVisual();
        _voiceTranscriptTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _voiceTranscriptTimer.Tick += (_, _) =>
        {
            _voiceTranscriptTimer.Stop();
            _voice.DismissTranscript();
        };
        _detailsTypingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(18) };
        _detailsTypingTimer.Tick += (_, _) => AdvanceDetailsTyping();

        ApplyPreferencesToControls();
        UpdateVoiceView();
        UpdateView();

        SizeChanged += (_, _) =>
        {
            UpdateResponsiveShellVisibility();
            UpdateMotionActivity();
        };
        Opened += async (_, _) => await InitializeProviderAsync(startupRoot);
    }

    public ExplorerSession Session => _session;

    public VisualPreferences Preferences => _preferences;

    public IOmniSorSeConnectionCoordinator Connection => _connection;

    public VoiceInteractionCoordinator Voice => _voice;

    public VoiceActionContext CaptureVoiceContext() => new(_session.ProviderGeneration);

    public bool IsVoiceContextCurrent(VoiceActionContext context) =>
        context.ProviderGeneration == _session.ProviderGeneration;

    public Task<VoiceActionResult> ExecuteVoiceIntentAsync(
        VoiceIntent intent,
        CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return ExecuteVoiceIntentCoreAsync(intent, cancellationToken);
        }

        var completion = new TaskCompletionSource<VoiceActionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                completion.TrySetResult(await ExecuteVoiceIntentCoreAsync(intent, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });
        return completion.Task;
    }

    public string CreateSanitizedDiagnosticsReport()
    {
        var graph = GraphScene.Diagnostics;
        var rain = DataRain.Diagnostics;
        var connection = _connection.Diagnostics;
        var voice = _voice.Diagnostics;
        var informationalVersion = typeof(MainWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "Unknown";
        var capabilities = _connection.ProtocolInfo?.Capabilities.ToString() ?? "None";

        return SanitizedDiagnosticsReport.Create(new SanitizedDiagnosticsSnapshot(
            informationalVersion,
            RuntimeInformation.OSDescription.Trim(),
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.RuntimeIdentifier,
            _session.ProviderDisplayName,
            connection.State.ToString(),
            _session.ViewMode.ToString(),
            connection.ProtocolVersion,
            connection.Transport,
            capabilities,
            _session.IsContextAvailable,
            _preferences.ReducedMotion,
            _preferences.ReducedEffects,
            voice.State.ToString(),
            voice.Provider,
            voice.ModelState,
            voice.InitializationDuration,
            voice.CaptureDuration,
            voice.TranscriptionDuration,
            voice.ExecutionDuration,
            voice.TranscriptLength,
            voice.Classification,
            voice.LastErrorCategory,
            graph.Nodes,
            _session.SceneBudget,
            graph.Edges,
            graph.Labels,
            graph.Zoom,
            graph.LayoutDuration,
            graph.ScenePreparationDuration,
            graph.LastRenderDuration,
            _session.LastLoadDuration,
            connection.LastRequestDuration,
            connection.TimeoutCount,
            connection.ReconnectCount,
            connection.StaleResponseRejectionCount,
            connection.LastFailureCategory,
            graph.RenderAllocatedBytes,
            graph.TextCacheEntries,
            graph.ResourceCacheEntries,
            rain.RenderedTokens,
            rain.LastRenderDuration));
    }

    private async Task InitializeProviderAsync(string? startupRoot)
    {
        if (!string.IsNullOrWhiteSpace(_handoffEndpoint))
        {
            var connected = await _connection.ConnectFromHandoffAsync(_handoffEndpoint);
            if (connected)
            {
                PopulateConnectedRoots();
                await OpenConnectedRootAsync(_connection.AccessibleRoots[0]);
                return;
            }

            ConnectionPanel.IsVisible = true;
        }

        if (!string.IsNullOrWhiteSpace(startupRoot))
        {
            await OpenRootAsync(startupRoot);
        }

        UpdateConnectionView();
        UpdateWelcomeAndStatusVisibility();
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

        _connection.UseStandalone();
        ConnectionPanel.IsVisible = false;
        await OpenRootAsync(path);
    }

    private void OnConnectionClick(object? sender, RoutedEventArgs e)
    {
        ConnectionPanel.IsVisible = !ConnectionPanel.IsVisible;
        if (ConnectionPanel.IsVisible)
        {
            CollapseSearchEditor();
            SettingsPanel.IsVisible = false;
            AccessibleListPanel.IsVisible = false;
            ContextFilterPanel.IsVisible = false;
            HistoryPanel.IsVisible = false;
            SearchResultsPanel.IsVisible = false;
            _detailsDismissed = true;
            DetailsPanel.IsVisible = false;
            PopulateConnectedRoots();
            UpdateConnectionView();
            if (ConnectedRootPicker.IsVisible)
            {
                ConnectedRootPicker.Focus();
            }
            else
            {
                ChooseStandaloneFolderButton.Focus();
            }
        }
        else
        {
            GraphScene.Focus();
        }

        UpdateWelcomeAndStatusVisibility();
    }

    private async void OnReconnectClick(object? sender, RoutedEventArgs e)
    {
        var connected = await _connection.RetryAsync();
        if (connected)
        {
            PopulateConnectedRoots();
            await OpenConnectedRootAsync(_connection.AccessibleRoots[0]);
        }

        UpdateConnectionView();
    }

    private void OnUseStandaloneClick(object? sender, RoutedEventArgs e)
    {
        _connection.UseStandalone();
        _session.Reset();
        ConnectedRootPicker.ItemsSource = null;
        ConnectionPanel.IsVisible = false;
        ChooseStandaloneFolderButton.Focus();
        UpdateConnectionView();
        UpdateWelcomeAndStatusVisibility();
    }

    private async void OnOpenConnectedRootClick(object? sender, RoutedEventArgs e)
    {
        if (ConnectedRootPicker.SelectedItem is not ConnectedRootItem item)
        {
            SetTransientStatus("Select an OmniSorSe-authorized root first.");
            return;
        }

        await OpenConnectedRootAsync(item.Node);
    }

    private async void OnStructureModeCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true })
        {
            await SwitchToStructureAsync();
        }
    }

    private async void OnStructureModeClick(object? sender, RoutedEventArgs e) =>
        await SwitchToStructureAsync();

    private async Task SwitchToStructureAsync()
    {
        if (_isApplyingPreferences || _isSwitchingViewMode || string.IsNullOrEmpty(_session.AccessRoot))
        {
            return;
        }

        _isSwitchingViewMode = true;
        try
        {
            await _session.SwitchToStructureAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isSwitchingViewMode = false;
        }
    }

    private async void OnContextModeCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true })
        {
            await SwitchToContextAsync();
        }
    }

    private async void OnContextModeClick(object? sender, RoutedEventArgs e) =>
        await SwitchToContextAsync();

    private async Task SwitchToContextAsync()
    {
        if (_isApplyingPreferences || _isSwitchingViewMode)
        {
            return;
        }

        if (string.IsNullOrEmpty(_session.AccessRoot))
        {
            SetTransientStatus("Connect to OmniSorSe and open an authorized root before exploring Context.");
            UpdateView();
            return;
        }

        _isSwitchingViewMode = true;
        try
        {
            await _session.SwitchToContextAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isSwitchingViewMode = false;
        }
    }

    private async void OnHybridModeCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true })
        {
            await SwitchToHybridAsync();
        }
    }

    private async void OnHybridModeClick(object? sender, RoutedEventArgs e) =>
        await SwitchToHybridAsync();

    private async Task SwitchToHybridAsync()
    {
        if (_isApplyingPreferences || _isSwitchingViewMode)
        {
            return;
        }

        if (string.IsNullOrEmpty(_session.AccessRoot))
        {
            SetTransientStatus("Connect to OmniSorSe and open an authorized root before exploring Hybrid.");
            UpdateView();
            return;
        }

        _isSwitchingViewMode = true;
        try
        {
            await _session.SwitchToHybridAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isSwitchingViewMode = false;
        }
    }

    private async Task OpenConnectedRootAsync(Protocol.ExplorerNode root)
    {
        if (_connection.Client is null || _connection.ProtocolInfo is null)
        {
            SetTransientStatus("OmniSorSe is not connected.");
            return;
        }

        try
        {
            var provider = new OmniSorSeConnectedProvider(_connection.Client, _connection.ProtocolInfo, root);
            await _session.OpenRootAsync(provider, provider);
            GraphScene.ResetView();
            ConnectionPanel.IsVisible = false;
        }
        catch (OperationCanceledException)
        {
            SetTransientStatus("Connected loading cancelled.");
        }
        catch (Exception exception) when (IsConnectionFailure(exception))
        {
            _connection.ReportDisconnected(exception);
            SetTransientStatus("OmniSorSe disconnected. The application remains available in standalone mode.");
        }
    }

    private void PopulateConnectedRoots()
    {
        var items = _connection.AccessibleRoots.Select(node => new ConnectedRootItem(node)).ToArray();
        ConnectedRootPicker.ItemsSource = items;
        ConnectedRootPicker.SelectedIndex = items.Length > 0 ? 0 : -1;
    }

    private async void OnBackClick(object? sender, RoutedEventArgs e) => await GoBackAsync();

    private async void OnUpClick(object? sender, RoutedEventArgs e) => await GoUpAsync();

    private async void OnRootClick(object? sender, RoutedEventArgs e) => await GoRootAsync();

    private async Task GoRootAsync()
    {
        try
        {
            if (await _session.GoRootAsync())
            {
                _sound.Play(InteractionSoundCue.Navigate);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task GoUpAsync()
    {
        try
        {
            if (await _session.GoUpAsync())
            {
                _sound.Play(InteractionSoundCue.Navigate);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnHistoryClick(object? sender, RoutedEventArgs e)
    {
        if (_session.NavigationHistoryCount == 0)
        {
            return;
        }

        var showHistory = !HistoryPanel.IsVisible;
        ConnectionPanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        AccessibleListPanel.IsVisible = false;
        ContextFilterPanel.IsVisible = false;
        HistoryPanel.IsVisible = false;
        CollapseSearchEditor();
        _detailsDismissed = true;
        DetailsPanel.IsVisible = false;
        HistoryPanel.IsVisible = showHistory;
        UpdateHistoryView();
        UpdateWelcomeAndStatusVisibility();
    }

    private void OnSoundToggleClick(object? sender, RoutedEventArgs e)
    {
        _preferences = (_preferences with { SoundEnabled = !_preferences.SoundEnabled }).Normalize();
        _preferencesStore.Save(_preferences);
        _sound.Enabled = _preferences.SoundEnabled;
        ApplyPreferencesToControls();
        if (_preferences.SoundEnabled)
        {
            _sound.Play(InteractionSoundCue.Select);
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        for (var visual = e.Source as Visual; visual is not null && visual != CustomTitleBar; visual = visual.GetVisualParent())
        {
            if (visual is Button)
            {
                return;
            }
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e) => ToggleMaximized();

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void OnCloseWindowClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximized() => WindowState = WindowState == WindowState.Maximized
        ? WindowState.Normal
        : WindowState.Maximized;

    private async Task GoBackAsync()
    {
        try
        {
            if (await _session.GoBackAsync())
            {
                _sound.Play(InteractionSoundCue.Navigate);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs e) => await RunSearchAsync();

    private void OnSearchToggleClick(object? sender, RoutedEventArgs e)
    {
        if (SearchEditor.IsVisible)
        {
            CollapseSearchEditor();
            SearchToggleButton.Focus();
            return;
        }

        OpenSearchEditor();
    }

    private void OpenSearchEditor()
    {
        ConnectionPanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        AccessibleListPanel.IsVisible = false;
        ContextFilterPanel.IsVisible = false;
        HistoryPanel.IsVisible = false;
        _detailsDismissed = true;
        DetailsPanel.IsVisible = false;
        SearchEditor.IsVisible = true;
        AutomationProperties.SetName(SearchToggleButton, "Collapse Search");
        UpdateWelcomeAndStatusVisibility();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void CollapseSearchEditor()
    {
        SearchEditor.IsVisible = false;
        AutomationProperties.SetName(SearchToggleButton, "Open Search");
        UpdateWelcomeAndStatusVisibility();
    }

    private void CloseSearchEditor(bool clearSearch)
    {
        if (clearSearch)
        {
            ClearSearch();
        }

        CollapseSearchEditor();
        SearchToggleButton.Focus();
    }

    private async void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await RunSearchAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseSearchEditor(clearSearch: true);
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

    private async Task<VoiceActionResult> ExecuteVoiceIntentCoreAsync(
        VoiceIntent intent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (intent.Kind)
        {
            case VoiceIntentKind.GoBack:
                if (!_session.CanGoBack)
                {
                    return VoiceActionResult.Rejected("There is no previous graph focus.");
                }

                await _session.GoBackAsync(cancellationToken);
                return VoiceActionResult.Completed("Moved back.");

            case VoiceIntentKind.GoUp:
                if (!await _session.GoUpAsync(cancellationToken))
                {
                    return VoiceActionResult.Rejected("The current focus has no authorized parent in this scope.");
                }

                _sound.Play(InteractionSoundCue.Navigate);
                return VoiceActionResult.Completed("Moved up one containment level.");

            case VoiceIntentKind.GoRoot:
                if (!await _session.GoRootAsync(cancellationToken))
                {
                    return VoiceActionResult.Rejected("The current focus is already at the authorized root.");
                }

                _sound.Play(InteractionSoundCue.Navigate);
                return VoiceActionResult.Completed("Moved to the authorized root.");

            case VoiceIntentKind.ActivateSelectedNode:
                var selectedNode = _session.SelectedNode;
                if (selectedNode is null)
                {
                    return VoiceActionResult.Rejected("Select a visible node before opening it.");
                }

                return await ActivateNodeAsync(selectedNode.Id)
                    ? VoiceActionResult.Completed($"Opened {selectedNode.Name}.")
                    : VoiceActionResult.Rejected("The selected item could not be opened safely.");

            case VoiceIntentKind.OpenVisibleNode:
            case VoiceIntentKind.FocusVisibleNode:
                return await ExecuteVisibleNodeVoiceIntentAsync(intent, cancellationToken);

            case VoiceIntentKind.ZoomIn:
                GraphScene.ZoomIn();
                return VoiceActionResult.Completed("Zoomed in.");

            case VoiceIntentKind.ZoomOut:
                GraphScene.ZoomOut();
                return VoiceActionResult.Completed("Zoomed out.");

            case VoiceIntentKind.ResetView:
                GraphScene.ResetView();
                return VoiceActionResult.Completed("View reset.");

            case VoiceIntentKind.SwitchToStructure:
                await SwitchToStructureAsync();
                return VoiceActionResult.Completed("Structure mode active.");

            case VoiceIntentKind.SwitchToContext:
            case VoiceIntentKind.ShowRelatedToFocus:
                if (!_session.IsContextAvailable)
                {
                    return VoiceActionResult.Rejected("Context exploration requires a connected OmniSorSe session.");
                }

                await SwitchToContextAsync();
                return VoiceActionResult.Completed("Context mode active.");

            case VoiceIntentKind.UseDarkTheme:
                SetThemeFromVoice("Dark");
                return VoiceActionResult.Completed("Dark mode active.");

            case VoiceIntentKind.UseLightTheme:
                SetThemeFromVoice("Light");
                return VoiceActionResult.Completed("Light mode active.");

            case VoiceIntentKind.OpenDetails:
                if (_session.SelectedNode is null)
                {
                    return VoiceActionResult.Rejected("Select a visible node before opening details.");
                }

                _detailsDismissed = false;
                UpdateView();
                DetailsPanel.Focus();
                return VoiceActionResult.Completed("Details opened.");

            case VoiceIntentKind.CloseDetails:
                _detailsDismissed = true;
                DetailsPanel.IsVisible = false;
                GraphScene.Focus();
                return VoiceActionResult.Completed("Details closed.");

            case VoiceIntentKind.ShowAccessibleList:
                ShowAccessibleList();
                return VoiceActionResult.Completed("Accessible list opened.");

            case VoiceIntentKind.HideAccessibleList:
                HideAccessibleList();
                return VoiceActionResult.Completed("Accessible list closed.");

            case VoiceIntentKind.ClearSearch:
                ClearSearch();
                return VoiceActionResult.Completed("Search cleared.");

            case VoiceIntentKind.Cancel:
                _session.CancelOperations();
                return VoiceActionResult.Completed("Current operation cancelled.");

            case VoiceIntentKind.Search:
            default:
                return await ExecuteVoiceSearchAsync(intent.Argument, cancellationToken);
        }
    }

    private async Task<VoiceActionResult> ExecuteVisibleNodeVoiceIntentAsync(
        VoiceIntent intent,
        CancellationToken cancellationToken)
    {
        var argument = intent.Argument?.Trim() ?? string.Empty;
        if (argument.Length == 0)
        {
            return VoiceActionResult.Rejected("No visible node name was recognized.");
        }

        var normalizedArgument = VoiceCommandParser.NormalizeForComparison(argument);
        var matches = _session.Neighborhood?.Nodes
            .Where(node => VoiceCommandParser.NormalizeForComparison(node.Name) == normalizedArgument)
            .ToArray() ?? [];
        if (matches.Length != 1)
        {
            return await ExecuteVoiceSearchAsync(argument, cancellationToken);
        }

        var node = matches[0];
        _detailsDismissed = false;
        _session.SelectNode(node.Id);
        if (intent.Kind == VoiceIntentKind.OpenVisibleNode)
        {
            return await ActivateNodeAsync(node.Id)
                ? VoiceActionResult.Completed($"Opened {node.Name}.")
                : VoiceActionResult.Rejected($"{node.Name} could not be opened safely.");
        }

        return VoiceActionResult.Completed($"Focused {node.Name}.");
    }

    private async Task<VoiceActionResult> ExecuteVoiceSearchAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        query = query?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return VoiceActionResult.Rejected("No Search query was recognized.");
        }

        if (string.IsNullOrEmpty(_session.AccessRoot))
        {
            return VoiceActionResult.Rejected("Choose a standalone root or connect to OmniSorSe before searching.");
        }

        SearchBox.Text = query;
        OpenSearchEditor();
        await _session.SearchAsync(query, cancellationToken);
        return VoiceActionResult.Completed(
            _session.ProviderMode == ExplorerProviderMode.Connected
                ? "OmniSorSe Search results are ready."
                : "Standalone structural Search results are ready.");
    }

    private void SetThemeFromVoice(string theme)
    {
        ThemePicker.SelectedIndex = theme == "Light" ? 1 : 0;
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
            SoundEnabled = SoundEnabledToggle.IsChecked == true,
        };
        _preferencesStore.Save(_preferences);
        ApplyVisualPreferences();
        UpdateView();
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var showSettings = !SettingsPanel.IsVisible;
        SettingsPanel.IsVisible = showSettings;
        if (showSettings)
        {
            CollapseSearchEditor();
            ConnectionPanel.IsVisible = false;
            AccessibleListPanel.IsVisible = false;
            ContextFilterPanel.IsVisible = false;
            SearchResultsPanel.IsVisible = false;
            _detailsDismissed = true;
            DetailsPanel.IsVisible = false;
        }

        UpdateView();
        (showSettings ? ReducedMotionToggle : SettingsButton).Focus();
    }

    private async void OnCopyDiagnosticsClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            DiagnosticsCopyStatus.Text = "Clipboard unavailable.";
            return;
        }

        try
        {
            await clipboard.SetTextAsync(CreateSanitizedDiagnosticsReport());
            DiagnosticsCopyStatus.Text = "Safe diagnostics copied. Review before sharing.";
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or COMException)
        {
            DiagnosticsCopyStatus.Text = "Could not access the clipboard.";
        }
    }

    private void OnCloseDetailsClick(object? sender, RoutedEventArgs e)
    {
        _detailsDismissed = true;
        DetailsPanel.IsVisible = false;
        UpdateResponsiveShellVisibility();
        GraphScene.Focus();
    }

    private void OnCloseSearchClick(object? sender, RoutedEventArgs e)
    {
        CloseSearchEditor(clearSearch: true);
    }

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => GraphScene.ZoomIn();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => GraphScene.ZoomOut();

    private void OnResetViewClick(object? sender, RoutedEventArgs e) => GraphScene.ResetView();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _session.CancelOperations();

    private void OnAccessibleListClick(object? sender, RoutedEventArgs e)
    {
        if (AccessibleListPanel.IsVisible)
        {
            HideAccessibleList();
            return;
        }

        ShowAccessibleList();
    }

    private void ShowAccessibleList()
    {
        CollapseSearchEditor();
        AccessibleListPanel.IsVisible = true;
        ConnectionPanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        SearchResultsPanel.IsVisible = false;
        ContextFilterPanel.IsVisible = false;
        SynchronizeAccessibleList();
        UpdateWelcomeAndStatusVisibility();
        AccessibleNodesList.Focus();
    }

    private void HideAccessibleList()
    {
        AccessibleListPanel.IsVisible = false;
        GraphScene.Focus();
        UpdateView();
    }

    private async void OnVoicePreferenceChanged(object? sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        SaveVoicePreferences();
        if (!_preferences.VoiceEnabled)
        {
            _voice.Disable();
            UpdateVoiceView();
            return;
        }

        await RefreshVoiceCapabilityAsync();
    }

    private async void OnVoiceLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        SaveVoicePreferences();
        if (_preferences.VoiceEnabled && !_voice.IsActive)
        {
            await RefreshVoiceCapabilityAsync();
        }
    }

    private void SaveVoicePreferences()
    {
        _preferences = (_preferences with
        {
            VoiceEnabled = VoiceEnabledToggle.IsChecked == true,
            VoiceLanguage = VoiceLanguagePicker.SelectedIndex == 1 ? "auto" : "en",
        }).Normalize();
        _preferencesStore.Save(_preferences);
    }

    private async Task RefreshVoiceCapabilityAsync()
    {
        try
        {
            await _voice.RefreshCapabilityAsync(_preferences.ToVoiceOptions());
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnVoiceButtonClick(object? sender, RoutedEventArgs e) =>
        await ToggleVoiceAsync();

    private async Task ToggleVoiceAsync()
    {
        if (!_preferences.VoiceEnabled)
        {
            _preferences = (_preferences with { VoiceEnabled = true }).Normalize();
            _preferencesStore.Save(_preferences);
            VoiceEnabledToggle.IsChecked = true;
        }

        try
        {
            if (_voice.State == VoiceCapabilityState.Listening)
            {
                await _voice.StopAsync(_preferences.ToVoiceOptions());
            }
            else if (_voice.IsActive)
            {
                await _voice.CancelAsync();
            }
            else
            {
                await _voice.StartAsync(_preferences.ToVoiceOptions());
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnVoiceCancelClick(object? sender, RoutedEventArgs e) =>
        await _voice.CancelAsync();

    private void OnVoiceStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateVoiceView();
        }
        else
        {
            Dispatcher.UIThread.Post(UpdateVoiceView);
        }
    }

    private void UpdateVoiceView()
    {
        if (_isDisposed)
        {
            return;
        }

        VoiceStateText.Text = _voice.Status;
        VoiceLevelIndicator.Value = _voice.InputLevel;
        VoiceTranscriptText.Text = string.IsNullOrWhiteSpace(_voice.TranscriptPreview)
            ? string.Empty
            : $"“{_voice.TranscriptPreview}”";
        VoiceTranscriptText.IsVisible = VoiceTranscriptText.Text.Length > 0;
        var showVoiceDetails = _voice.State != VoiceCapabilityState.Disabled || VoiceTranscriptText.IsVisible;
        VoiceListeningRing.IsVisible = showVoiceDetails;
        VoiceLevelIndicator.IsVisible = showVoiceDetails;
        VoiceStatusStack.IsVisible = showVoiceDetails;
        if (VoiceTranscriptText.IsVisible)
        {
            _voiceTranscriptTimer.Start();
        }
        else
        {
            _voiceTranscriptTimer.Stop();
        }
        VoiceCancelButton.IsVisible = _voice.IsActive;
        VoiceButton.Content = _voice.State switch
        {
            VoiceCapabilityState.Listening => "Listening… Stop",
            VoiceCapabilityState.Loading or VoiceCapabilityState.Transcribing or VoiceCapabilityState.Executing => "Cancel voice",
            VoiceCapabilityState.Disabled => "Listen",
            _ => "Listen",
        };
        var stateName = _voice.State.ToString();
        AutomationProperties.SetName(VoiceButton, $"Toggle local listening. Voice state: {stateName}");
        AutomationProperties.SetHelpText(
            VoiceButton,
            "Press once to begin bounded microphone capture and again to stop and transcribe locally. Keyboard shortcut Control Shift Space.");
        AutomationProperties.SetName(VoiceHud, $"Voice input. {_voice.Status}");

        var shouldAnimate = _voice.State == VoiceCapabilityState.Listening &&
            !_preferences.ReducedMotion &&
            !_preferences.ReducedEffects;
        if (shouldAnimate)
        {
            _voiceVisualTimer.Start();
        }
        else
        {
            _voiceVisualTimer.Stop();
            VoiceListeningRing.Opacity = _voice.State == VoiceCapabilityState.Listening ? 1 : 0.55;
        }
    }

    private void UpdateVoiceListeningVisual()
    {
        if (_voice.State != VoiceCapabilityState.Listening ||
            _preferences.ReducedMotion ||
            _preferences.ReducedEffects)
        {
            _voiceVisualTimer.Stop();
            VoiceListeningRing.Opacity = _voice.State == VoiceCapabilityState.Listening ? 1 : 0.55;
            return;
        }

        _voicePulseHigh = !_voicePulseHigh;
        VoiceListeningRing.Opacity = _voicePulseHigh ? 1 : 0.55;
    }

    private void OnContextFilterClick(object? sender, RoutedEventArgs e)
    {
        if (!IsContextualMode(_session.ViewMode))
        {
            return;
        }

        ContextFilterPanel.IsVisible = !ContextFilterPanel.IsVisible;
        if (ContextFilterPanel.IsVisible)
        {
            CollapseSearchEditor();
            ConnectionPanel.IsVisible = false;
            SettingsPanel.IsVisible = false;
            AccessibleListPanel.IsVisible = false;
            SearchResultsPanel.IsVisible = false;
            _detailsDismissed = true;
            DetailsPanel.IsVisible = false;
            ApplyContextFilterToControls();
            ContextKindFilter.Focus();
        }
        else
        {
            ContextFilterButton.Focus();
            UpdateView();
        }

        UpdateWelcomeAndStatusVisibility();
    }

    private void OnContextFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingContextFilters || !IsContextualMode(_session.ViewMode))
        {
            return;
        }

        _session.ApplyContextFilter(new ContextFilter(
            KindFromIndex(ContextKindFilter.SelectedIndex),
            StrengthFromIndex(ContextStrengthFilter.SelectedIndex),
            EvidenceFromIndex(ContextEvidenceFilter.SelectedIndex)));
    }

    private void OnClearContextFiltersClick(object? sender, RoutedEventArgs e)
    {
        _session.ClearContextFilter();
        ApplyContextFilterToControls();
    }

    private void OnAccessibleNodeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingAccessibleList || AccessibleNodesList.SelectedItem is not AccessibleNodeItem item)
        {
            return;
        }

        _detailsDismissed = false;
        _session.SelectNode(item.NodeId);
    }

    private async void OnAccessibleNodeDoubleTapped(object? sender, TappedEventArgs e) =>
        await ActivateSelectedAccessibleNodeAsync();

    private async void OnOpenAccessibleNodeClick(object? sender, RoutedEventArgs e) =>
        await ActivateSelectedAccessibleNodeAsync();

    private async void OnAccessibleNodesKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ActivateSelectedAccessibleNodeAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            await GoBackAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            AccessibleListPanel.IsVisible = false;
            UpdateWelcomeAndStatusVisibility();
            GraphScene.Focus();
            e.Handled = true;
        }
    }

    private async Task ActivateSelectedAccessibleNodeAsync()
    {
        if (AccessibleNodesList.SelectedItem is not AccessibleNodeItem item)
        {
            SetTransientStatus("Select a structural item first.");
            return;
        }

        await ActivateNodeAsync(item.NodeId);
    }

    private void OnGraphNodeSelected(object? sender, string nodeId)
    {
        _detailsDismissed = false;
        SettingsPanel.IsVisible = false;
        _session.SelectNode(nodeId);
        _sound.Play(InteractionSoundCue.Select);
    }

    private void OnGraphNodeHovered(object? sender, string? nodeId)
    {
        if (nodeId is not null)
        {
            _sound.Play(InteractionSoundCue.Hover);
        }
    }

    private async void OnGraphNodeActivated(object? sender, string nodeId)
    {
        await ActivateNodeAsync(nodeId);
    }

    private async Task<bool> ActivateNodeAsync(string nodeId)
    {
        var node = _session.Neighborhood?.Nodes.FirstOrDefault(item =>
            ExplorerIdentity.Equals(item.Id, nodeId));
        if (node is null)
        {
            return false;
        }

        if (node.Kind == ExplorerNodeKind.Aggregate)
        {
            if (!_session.ActivateAggregate(node.Id))
            {
                SetTransientStatus("This aggregate is informational because the source enumeration was bounded.");
            }

            return true;
        }

        if (IsContextualMode(_session.ViewMode) &&
            !ExplorerIdentity.Equals(node.Id, _session.Neighborhood?.FocusNodeId))
        {
            try
            {
                if (_session.ViewMode == ExplorerViewMode.Hybrid)
                {
                    await _session.FocusHybridNodeAsync(node.Target);
                }
                else
                {
                    await _session.FocusContextNodeAsync(node.Target);
                }
            }
            catch (OperationCanceledException)
            {
            }

            return true;
        }

        if (node.Kind is ExplorerNodeKind.Folder or ExplorerNodeKind.Context && node.IsNavigable)
        {
            try
            {
                var opened = await _session.NavigateAsync(node.Target);
                if (opened)
                {
                    _sound.Play(InteractionSoundCue.FolderEnter);
                }

                return opened;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        if (node.Kind == ExplorerNodeKind.File)
        {
            if (_session.ProviderMode != ExplorerProviderMode.Standalone)
            {
                SetTransientStatus("Connected file opening is unavailable because projected paths are not filesystem authority.");
                return false;
            }

            var result = await _fileActivation.OpenAsync(_session.AccessRoot, node.Path);
            SetTransientStatus(result switch
            {
                FileActivationResult.Opened => $"Opened {node.Name} with its Windows app.",
                FileActivationResult.HighRiskTypeBlocked => "Executable and script-like files are not launched from the spatial graph.",
                FileActivationResult.ReparsePointBlocked => "This reparse-point file was not launched.",
                FileActivationResult.NotFound => "The selected file is no longer available.",
                FileActivationResult.OutsideAccessRoot => "The selected file is outside the authorized root.",
                _ => "Windows could not open the selected file safely.",
            });
            if (result == FileActivationResult.Opened)
            {
                _sound.Play(InteractionSoundCue.FileOpen);
                return true;
            }
        }

        return false;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space &&
            e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _ = ToggleVoiceAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F &&
            e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (IsContextualMode(_session.ViewMode))
            {
                OnContextFilterClick(ContextFilterButton, new RoutedEventArgs());
            }

            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OpenSearchEditor();
            e.Handled = true;
        }
        else if ((e.Key == Key.Left && e.KeyModifiers.HasFlag(KeyModifiers.Alt)) || e.Key == Key.BrowserBack)
        {
            _ = GoBackAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Up && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            _ = GoUpAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Home && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            _ = GoRootAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.I && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (_session.SelectedNode is not null)
            {
                _detailsDismissed = false;
                UpdateView();
                DetailsPanel.Focus();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.L &&
                 e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                 e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            OnAccessibleListClick(AccessibleListButton, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.D1 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnStructureModeClick(StructureModeButton, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.D2 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnContextModeClick(ContextModeButton, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.D3 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnHybridModeClick(HybridModeButton, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_voice.IsActive)
            {
                _ = _voice.CancelAsync();
            }
            else
            {
                DismissTransientSurfaces();
            }

            e.Handled = true;
        }
    }

    private void OnSessionStateChanged(object? sender, EventArgs e) => UpdateView();

    private void OnConnectionStateChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(UpdateConnectionView);

    private void OnProviderFailed(Exception exception) => Dispatcher.UIThread.Post(() =>
    {
        if (_session.ProviderMode == ExplorerProviderMode.Connected)
        {
            _connection.ReportDisconnected(exception);
        }

        UpdateConnectionView();
    });

    private void UpdateView()
    {
        var neighborhood = _session.Neighborhood;
        var selected = _session.SelectedNode;
        var currentPath = string.IsNullOrEmpty(_session.CurrentPath)
            ? "No folder selected"
            : _session.CurrentPath;
        CurrentPathText.Text = neighborhood?.Focus.Name ?? currentPath;
        ToolTip.SetTip(CurrentPathText, currentPath);
        AutomationProperties.SetName(FocusHud, $"Current focus: {CurrentPathText.Text}");
        AutomationProperties.SetHelpText(FocusHud, currentPath);
        StatusText.Text = _session.Status;
        AnnounceStatus(_session.Status);
        ViewModeStatusText.Text = _session.ViewMode.ToString().ToUpperInvariant();
        Title = $"OmniBrille — {_session.ViewMode}";
        _isApplyingPreferences = true;
        try
        {
            StructureModeButton.IsChecked = _session.ViewMode == ExplorerViewMode.Structure;
            ContextModeButton.IsChecked = _session.ViewMode == ExplorerViewMode.Context;
            HybridModeButton.IsChecked = _session.ViewMode == ExplorerViewMode.Hybrid;
        }
        finally
        {
            _isApplyingPreferences = false;
        }

        var connectedSearch = _session.ProviderMode == ExplorerProviderMode.Connected;
        SearchBox.PlaceholderText = connectedSearch
            ? "Search authorized indexed scope"
            : "Search selected root";
        AutomationProperties.SetName(
            SearchBox,
            connectedSearch
                ? $"{_session.ViewMode} Search through OmniSorSe"
                : _session.ViewMode switch
                {
                    ExplorerViewMode.Context => "Context search",
                    ExplorerViewMode.Hybrid => "Hybrid search",
                    _ => "Structural search",
                });
        AutomationProperties.SetHelpText(
            SearchBox,
            connectedSearch
                ? "Searches the current authorized OmniSorSe session scope. Press Escape to clear."
                : "Searches only names and paths inside the selected root. Press Escape to clear.");
        AutomationProperties.SetName(
            SearchButton,
            connectedSearch ? "Run OmniSorSe Search" : "Run Standalone Search");
        AutomationProperties.SetName(
            SearchResultsList,
            connectedSearch ? "OmniSorSe Search results" : "Standalone Search results");
        AutomationProperties.SetName(
            GraphScene,
            $"Spatial {_session.ViewMode} graph");
        AutomationProperties.SetName(
            AccessibleNodesList,
            $"Visible {_session.ViewMode} graph nodes");
        AutomationProperties.SetName(
            AccessibleOpenButton,
            IsContextualMode(_session.ViewMode) ? "Focus selected graph node" : "Open selected structural node");
        BackButton.IsEnabled = _session.CanGoBack && !_session.IsLoading;
        UpButton.IsEnabled = _session.CanGoUp && !_session.IsLoading;
        ChooseFolderButton.IsEnabled = _session.CanGoRoot && !_session.IsLoading;
        HistoryButton.IsEnabled = _session.NavigationHistoryCount > 0;
        HistoryButton.Content = $"TRAIL {_session.NavigationHistoryCount}";
        SoundButton.Content = _preferences.SoundEnabled ? "SOUND ON" : "SOUND OFF";
        AutomationProperties.SetName(
            SoundButton,
            _preferences.SoundEnabled ? "Mute interaction sounds" : "Enable interaction sounds");
        UpdateHistoryView();
        ContextFilterButton.IsVisible = IsContextualMode(_session.ViewMode);
        if (!IsContextualMode(_session.ViewMode))
        {
            ContextFilterPanel.IsVisible = false;
        }
        ContextFilterButton.Content = _session.ContextFilter.IsActive ? "Filter · active" : "Filter";
        AutomationProperties.SetName(
            ContextFilterButton,
            _session.ContextFilter.IsActive ? "Open Context filters, filters active" : "Open Context filters");
        ApplyContextFilterToControls();

        var initialLoading = _session.IsLoading && neighborhood is null;
        InitialLoadingOverlay.IsVisible = initialLoading;
        InitialLoadingText.Text = _session.Status;
        DataRain.SetActive(initialLoading);
        ProgressHud.IsVisible = (neighborhood is not null && _session.IsLoading) || _session.IsSearching;
        ProgressText.Text = _session.IsSearching
            ? _session.ProviderMode == ExplorerProviderMode.Connected
                ? "Searching OmniSorSe…"
                : "Searching selected root…"
            : _session.ViewMode == ExplorerViewMode.Context
                ? "Reading authoritative Context…"
            : _session.ViewMode == ExplorerViewMode.Hybrid
                ? "Composing bounded Structure and Context…"
            : _session.LoadState == ExplorerLoadState.PartiallyLoaded
                ? $"Graph interactive · {_session.LoadedItemCount:N0} items streamed"
                : _session.ProviderMode == ExplorerProviderMode.Connected
                    ? "Reading indexed structure…"
                    : "Reading structural items…";

        var results = _session.SearchResult;
        var showSearchResults = results is not null &&
            _session.SearchQuery.Length > 0 &&
            !AccessibleListPanel.IsVisible &&
            !ContextFilterPanel.IsVisible &&
            !SettingsPanel.IsVisible &&
            !ConnectionPanel.IsVisible;
        DetailsPanel.IsVisible = selected is not null &&
            !_detailsDismissed &&
            !SettingsPanel.IsVisible &&
            !ConnectionPanel.IsVisible &&
            !ContextFilterPanel.IsVisible &&
            !showSearchResults;
        if (selected is not null)
        {
            UpdateDetails(selected, neighborhood);
        }

        SearchResultsList.ItemsSource = results?.Hits;
        SearchResultsPanel.IsVisible = showSearchResults;
        if (showSearchResults)
        {
            SearchEditor.IsVisible = true;
            AutomationProperties.SetName(SearchToggleButton, "Collapse Search");
        }
        var hasSearchMatches = results is { Hits.Count: > 0 };
        SearchResultsList.IsVisible = hasSearchMatches;
        SearchEmptyState.IsVisible = results is not null && !hasSearchMatches;
        FocusSearchResultButton.IsEnabled = hasSearchMatches;
        SearchSummaryText.Text = results is null
            ? "Search results"
            : $"{results.Hits.Count:N0} MATCHES{(results.WasTruncated ? " · BOUNDED" : string.Empty)}";
        var emptyStructure = _session.ViewMode == ExplorerViewMode.Structure &&
            neighborhood is not null &&
            !_session.IsLoading &&
            neighborhood.TotalChildCount == 0 &&
            string.IsNullOrWhiteSpace(neighborhood.Warning);
        StructureEmptyPanel.IsVisible = emptyStructure && !showSearchResults;
        StructureEmptyChooseButton.IsVisible = _session.ProviderMode == ExplorerProviderMode.Standalone;
        StructureEmptyProviderButton.IsVisible = _session.ProviderMode == ExplorerProviderMode.Connected;
        StructureEmptyBackButton.IsEnabled = _session.CanGoBack && !_session.IsLoading;
        StructureEmptyText.Text = _session.ProviderMode == ExplorerProviderMode.Connected
            ? "This authorized indexed folder contains no visible items. Go Back or choose another authorized root."
            : "This folder contains no visible items. Choose another folder or go Back.";
        var emptyContext = IsContextualMode(_session.ViewMode) &&
            !_session.IsLoading &&
            _session.ContextFilterSummary?.MatchingRelationshipCount == 0;
        ContextEmptyPanel.IsVisible = emptyContext && !ContextFilterPanel.IsVisible;
        ContextEmptyClearButton.IsVisible = _session.ContextFilter.IsActive;
        ContextEmptyText.Text = _session.ContextFilter.IsActive
            ? "No relationships match these filters. Clear them to restore the authorized Context neighborhood."
            : _session.ViewMode == ExplorerViewMode.Hybrid
                ? "No contextual relationships found for this item. Structural orientation remains available; OmniBrille does not invent nearby relationships."
                : "No contextual relationships found for this item. OmniBrille does not invent nearby relationships.";

        GraphScene.ReducedMotion = _preferences.ReducedMotion;
        GraphScene.ReducedEffects = _preferences.ReducedEffects;
        GraphScene.TextScale = Math.Clamp(FontSize / 13d, 1, 2);
        var graphSearchActive = results is not null && _session.SearchQuery.Length > 0;
        GraphScene.SearchActive = graphSearchActive;
        DataRain.ReducedMotion = _preferences.ReducedMotion;
        DataRain.ReducedEffects = _preferences.ReducedEffects;
        var neighborhoodChanged = !ReferenceEquals(neighborhood, _lastRenderedNeighborhood);
        if (neighborhoodChanged)
        {
            GraphScene.SetScene(
                neighborhood,
                selected?.Id,
                _session.HighlightedNodeIds,
                animate: !_preferences.ReducedMotion);
        }
        else
        {
            GraphScene.SetInteractionState(selected?.Id, _session.HighlightedNodeIds, graphSearchActive);
        }
        _lastRenderedNeighborhood = neighborhood;
        SynchronizeAccessibleList();
        DiagnosticsPanel.IsVisible = _preferences.DiagnosticsVisible;
        UpdateDiagnostics();
        UpdateConnectionView();
        UpdateWelcomeAndStatusVisibility();
    }

    private void UpdateConnectionView()
    {
        ConnectionButton.Content = _connection.UserStatus;
        AutomationProperties.SetName(ConnectionButton, $"Provider status: {_connection.UserStatus}");
        ConnectionStatusText.Text = _connection.UserStatus;
        var connected = _connection.State == OmniSorSeConnectionState.Connected;
        ConnectedRootPicker.IsVisible = connected && _connection.AccessibleRoots.Count > 0;
        OpenConnectedRootButton.IsVisible = connected && _connection.AccessibleRoots.Count > 0;
        ReconnectButton.IsVisible = _connection.State is
            OmniSorSeConnectionState.Disconnected or
            OmniSorSeConnectionState.Unavailable or
            OmniSorSeConnectionState.Error or
            OmniSorSeConnectionState.Incompatible;
        ConnectionDescriptionText.Text = _connection.State switch
        {
            OmniSorSeConnectionState.Connected =>
                $"Explorer Protocol {_connection.ProtocolInfo?.ProtocolMajor}.{_connection.ProtocolInfo?.ProtocolMinor} · {_connection.AccessibleRoots.Count:N0} authorized indexed root(s) · read-only.",
            OmniSorSeConnectionState.Incompatible =>
                "This version of OmniBrille is not compatible with the running OmniSorSe version. Standalone mode remains available.",
            OmniSorSeConnectionState.Disconnected =>
                "The previous graph is retained as stale context. Retry while the short-lived session is still valid, or switch to standalone.",
            OmniSorSeConnectionState.Unavailable when
                string.Equals(_connection.Diagnostics.LastFailureCategory, "Session expired", StringComparison.Ordinal) =>
                "The 15-minute OmniSorSe session expired. The previous graph is stale; launch OmniBrille from OmniSorSe again for a fresh grant, or use standalone.",
            OmniSorSeConnectionState.Standalone =>
                "Choose a folder for explicit standalone access. Connected mode begins only from an authorized OmniSorSe launch handoff.",
            _ =>
                "A compatible OmniSorSe release can launch OmniBrille through a one-time authorized local handoff; standalone remains available after any failure.",
        };
    }

    private void UpdateDetails(ExplorerNode selected, ExplorerNeighborhood? neighborhood)
    {
        var connectedDetails = _session.SelectedNodeDetails;
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
        DetailsModifiedText.Text = (connectedDetails?.ModifiedAt ?? selected.LastModified)
            ?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "—";
        DetailsAccessText.Text = selected.IsNavigable
            ? _session.ViewMode == ExplorerViewMode.Hybrid
                ? $"Navigable · {DescribeRoles(selected)}"
                : "Navigable"
            : selected.Kind == ExplorerNodeKind.File
                ? "Read-only metadata"
                : "Bounded / informational";
        DetailsPathText.Text = selected.Path;
        DetailsHintText.Text = selected.Kind == ExplorerNodeKind.Aggregate
            ? selected.AggregateAction?.Description ?? "Enumeration was bounded before this aggregate could be refined."
            : _session.ProviderMode == ExplorerProviderMode.Connected
                ? _session.ViewMode == ExplorerViewMode.Hybrid
                    ? "Read-only indexed data. Solid edges show Structure; dashed edges show OmniSorSe-authored Context."
                    : "Read-only indexed data supplied by OmniSorSe Explorer Protocol v1."
                : "Single-click selects and inspects. Double-click enters a folder or opens an ordinary file with Windows. Reparse points and executable/script-like files are never launched.";
        DetailsIndexText.Text = connectedDetails is null
            ? _session.ProviderMode == ExplorerProviderMode.Connected ? "Loading…" : "Not applicable"
            : connectedDetails.IsFullyIndexed ? "Complete" : "Incomplete";
        DetailsSummaryText.Text = connectedDetails?.Summary ?? string.Empty;
        var relationship = _session.SelectedRelationship;
        RelationshipDetailsSection.IsVisible = relationship is not null;
        AutomationProperties.SetHelpText(RelationshipDetailsSection, null);
        AutomationProperties.SetHelpText(DetailsPanel, null);
        AutomationProperties.SetName(DetailsRelationshipText, "Relationship reason");
        AutomationProperties.SetName(DetailsRelationshipStrengthText, "Relationship ranking strength");
        AutomationProperties.SetName(DetailsRelationshipEvidenceText, "Relationship evidence class");
        AutomationProperties.SetName(DetailsProvenanceText, "Relationship provenance");
        if (relationship is not null)
        {
            DetailsRelationshipText.Text = string.IsNullOrWhiteSpace(relationship.Reason)
                ? $"{relationship.Kind} relationship; OmniSorSe supplied no additional reason."
                : relationship.Reason;
            DetailsRelationshipStrengthText.Text = $"{StrengthLabel(relationship.Strength)} · {relationship.Strength}/100";
            DetailsRelationshipEvidenceText.Text = relationship.EvidenceClass.ToString();
            DetailsProvenanceText.Text = string.IsNullOrWhiteSpace(relationship.Provenance)
                ? "Not supplied"
                : FriendlyProvenance(relationship.Provenance);
            AutomationProperties.SetName(
                DetailsRelationshipText,
                $"Relationship reason: {DetailsRelationshipText.Text}");
            AutomationProperties.SetName(
                DetailsRelationshipStrengthText,
                $"Relationship ranking strength: {DetailsRelationshipStrengthText.Text}");
            AutomationProperties.SetName(
                DetailsRelationshipEvidenceText,
                $"Relationship evidence class: {DetailsRelationshipEvidenceText.Text}");
            AutomationProperties.SetName(
                DetailsProvenanceText,
                $"Relationship provenance: {DetailsProvenanceText.Text}");
            var accessibleRelationshipSummary =
                $"Related because {DetailsRelationshipText.Text}. " +
                $"Strength {DetailsRelationshipStrengthText.Text}. " +
                $"Evidence {DetailsRelationshipEvidenceText.Text}. " +
                $"Source {DetailsProvenanceText.Text}.";
            AutomationProperties.SetHelpText(RelationshipDetailsSection, accessibleRelationshipSummary);
            AutomationProperties.SetHelpText(DetailsPanel, accessibleRelationshipSummary);
        }

        var terminal = string.Join(
            Environment.NewLine,
            $"> inspect --node \"{selected.Name}\"",
            $"TYPE  {DetailsTypeText.Text}",
            $"ACCESS  {DetailsAccessText.Text}",
            $"INDEX  {DetailsIndexText.Text}",
            "STATUS  READY");
        AutomationProperties.SetName(DetailsTerminalText, terminal.Replace(Environment.NewLine, ". "));
        StartDetailsTyping(selected.Id, terminal);
    }

    private void StartDetailsTyping(string nodeId, string terminal)
    {
        if (ExplorerIdentity.Equals(_lastDetailsNodeId, nodeId) &&
            string.Equals(_detailsTypingTarget, terminal, StringComparison.Ordinal))
        {
            return;
        }

        _lastDetailsNodeId = nodeId;
        _detailsTypingTarget = terminal;
        _detailsTypingIndex = 0;
        _detailsTypingTimer.Stop();
        if (_preferences.ReducedMotion)
        {
            DetailsTerminalText.Text = terminal;
            return;
        }

        DetailsTerminalText.Text = string.Empty;
        _detailsTypingTimer.Start();
    }

    private void AdvanceDetailsTyping()
    {
        if (_preferences.ReducedMotion || _detailsTypingIndex >= _detailsTypingTarget.Length)
        {
            DetailsTerminalText.Text = _detailsTypingTarget;
            _detailsTypingTimer.Stop();
            return;
        }

        _detailsTypingIndex = Math.Min(_detailsTypingTarget.Length, _detailsTypingIndex + 4);
        DetailsTerminalText.Text = _detailsTypingTarget[.._detailsTypingIndex];
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
            SoundEnabledToggle.IsChecked = _preferences.SoundEnabled;
            VoiceEnabledToggle.IsChecked = _preferences.VoiceEnabled;
            VoiceLanguagePicker.SelectedIndex = _preferences.VoiceLanguage == "auto" ? 1 : 0;
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
        _sound.Enabled = _preferences.SoundEnabled;
        SoundButton.Content = _preferences.SoundEnabled ? "SOUND ON" : "SOUND OFF";
        if (_preferences.DiagnosticsVisible)
        {
            _diagnosticsTimer.Start();
        }
        else
        {
            _diagnosticsTimer.Stop();
        }

        if (_preferences.ReducedMotion && _detailsTypingTimer.IsEnabled)
        {
            DetailsTerminalText.Text = _detailsTypingTarget;
            _detailsTypingTimer.Stop();
        }
        UpdateVoiceView();
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
        var rainDiagnostics = DataRain.Diagnostics;
        var connectionDiagnostics = _connection.Diagnostics;
        var voiceDiagnostics = _voice.Diagnostics;
        DiagnosticsText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"provider {_session.ProviderDisplayName}  connection {connectionDiagnostics.State}  protocol {connectionDiagnostics.ProtocolVersion}  transport {connectionDiagnostics.Transport}\n" +
            $"nodes {diagnostics.Nodes}/{_session.SceneBudget}  edges {diagnostics.Edges}  labels {diagnostics.Labels}  zoom {diagnostics.Zoom:0.00}\n" +
            $"layout {diagnostics.LayoutDuration.TotalMilliseconds:0.00} ms  prep {diagnostics.ScenePreparationDuration.TotalMilliseconds:0.00} ms  render {diagnostics.LastRenderDuration.TotalMilliseconds:0.00} ms  load {_session.LastLoadDuration.TotalMilliseconds:0.0} ms\n" +
            $"bg {diagnostics.BackgroundDuration.TotalMilliseconds:0.00}  edge {diagnostics.EdgeDuration.TotalMilliseconds:0.00}  glyph {diagnostics.GlyphDuration.TotalMilliseconds:0.00}  label {diagnostics.LabelPreparationDuration.TotalMilliseconds + diagnostics.LabelDrawDuration.TotalMilliseconds:0.00} ms\n" +
            $"ipc {connectionDiagnostics.LastRequestDuration.TotalMilliseconds:0.0} ms  response {connectionDiagnostics.LastResponseNodeCount} nodes  search {connectionDiagnostics.LastSearchResultCount}  timeouts {connectionDiagnostics.TimeoutCount}  reconnects {connectionDiagnostics.ReconnectCount}  stale {connectionDiagnostics.StaleResponseRejectionCount}\n" +
            $"alloc {diagnostics.RenderAllocatedBytes / 1024d:0.0} KiB  caches text {diagnostics.TextCacheEntries}/256 resources {diagnostics.ResourceCacheEntries}/576  rain {rainDiagnostics.RenderedTokens} @ {rainDiagnostics.LastRenderDuration.TotalMilliseconds:0.00} ms\n" +
            $"voice {voiceDiagnostics.State}  provider {voiceDiagnostics.Provider}  init {voiceDiagnostics.InitializationDuration.TotalMilliseconds:0.0} ms  capture {voiceDiagnostics.CaptureDuration.TotalMilliseconds:0.0} ms  transcribe {voiceDiagnostics.TranscriptionDuration.TotalMilliseconds:0.0} ms  execute {voiceDiagnostics.ExecutionDuration.TotalMilliseconds:0.0} ms  chars {voiceDiagnostics.TranscriptLength}  class {voiceDiagnostics.Classification}");
    }

    private void ClearSearch()
    {
        SearchBox.Text = string.Empty;
        _session.ClearSearch();
    }

    private void SynchronizeAccessibleList()
    {
        var neighborhood = _session.Neighborhood;
        AccessibleFocusText.Text = neighborhood?.Focus.Path ?? "No folder selected";
        var selectedId = _session.SelectedNode?.Id;
        var highlights = _session.HighlightedNodeIds;
        var items = neighborhood?.Nodes.Select(node =>
        {
            var isFocus = ExplorerIdentity.Equals(node.Id, neighborhood.FocusNodeId);
            var isSelected = ExplorerIdentity.Equals(node.Id, selectedId);
            var isMatch = highlights.Contains(node.Id);
            var stateParts = new[]
            {
                isFocus ? "FOCUS" : null,
                isMatch ? "MATCH" : null,
                isSelected ? "SELECTED" : null,
            }.Where(value => value is not null).ToArray();
            var state = string.Join(" · ", stateParts);
            var relationship = neighborhood.Edges
                .Where(edge => edge.Kind == ExplorerGraphEdgeKind.Contextual && edge.Relationship is not null)
                .Where(edge =>
                    (ExplorerIdentity.Equals(edge.SourceId, neighborhood.FocusNodeId) && ExplorerIdentity.Equals(edge.TargetId, node.Id)) ||
                    (ExplorerIdentity.Equals(edge.TargetId, neighborhood.FocusNodeId) && ExplorerIdentity.Equals(edge.SourceId, node.Id)))
                .OrderByDescending(edge => edge.Relationship!.Strength)
                .Select(edge => edge.Relationship)
                .FirstOrDefault();
            var kind = node.Kind switch
            {
                ExplorerNodeKind.Context => "Previous folder",
                ExplorerNodeKind.Aggregate => "Aggregate",
                ExplorerNodeKind.Folder => "Folder",
                ExplorerNodeKind.File => "File",
                _ => "Node",
            };
            var accessibleState = state.Length == 0
                ? string.Empty
                : $", {string.Join(", ", stateParts).ToLowerInvariant()}";
            var sceneRelation = ExplorerSceneSemantics.Describe(neighborhood, node);
            var role = DescribeRoles(node);
            var roleDescription = $", {sceneRelation}";
            var relationDescription = relationship is null
                ? string.Empty
                : string.IsNullOrWhiteSpace(relationship.Reason)
                    ? ", contextually related"
                    : $", contextually related: {relationship.Reason}";
            return new AccessibleNodeItem(
                node.Id,
                node.Name,
                neighborhood.ViewMode == ExplorerViewMode.Hybrid
                    ? $"{kind} · {sceneRelation} · {role} · {node.Path}"
                    : $"{kind} · {sceneRelation} · {node.Path}",
                state,
                $"{node.Name}, {kind}{roleDescription}{accessibleState}{relationDescription}");
        }).ToArray() ?? [];

        _isSynchronizingAccessibleList = true;
        try
        {
            AccessibleNodesList.ItemsSource = items;
            AccessibleNodesList.SelectedItem = items.FirstOrDefault(item =>
                ExplorerIdentity.Equals(item.NodeId, selectedId));
        }
        finally
        {
            _isSynchronizingAccessibleList = false;
        }
    }

    private void DismissTransientSurfaces()
    {
        if (_voice.IsActive)
        {
            _ = _voice.CancelAsync();
        }

        if (_session.IsLoading || _session.IsSearching)
        {
            _session.CancelOperations();
        }

        SettingsPanel.IsVisible = false;
        ConnectionPanel.IsVisible = false;
        AccessibleListPanel.IsVisible = false;
        ContextFilterPanel.IsVisible = false;
        CollapseSearchEditor();
        _detailsDismissed = true;
        DetailsPanel.IsVisible = false;
        if (_session.SearchResult is not null)
        {
            ClearSearch();
        }

        GraphScene.Focus();
        UpdateWelcomeAndStatusVisibility();
    }

    private void UpdateWelcomeAndStatusVisibility()
    {
        var panelOwnsStatusSpace = ConnectionPanel.IsVisible ||
            SettingsPanel.IsVisible ||
            AccessibleListPanel.IsVisible ||
            ContextFilterPanel.IsVisible ||
            HistoryPanel.IsVisible ||
            SearchResultsPanel.IsVisible;

        WelcomePanel.IsVisible = _session.Neighborhood is null &&
            !_session.IsLoading &&
            !panelOwnsStatusSpace &&
            !SearchEditor.IsVisible &&
            !DetailsPanel.IsVisible;
        StatusHud.IsVisible = !panelOwnsStatusSpace;
        UpdateResponsiveShellVisibility();
    }

    private void UpdateResponsiveShellVisibility()
    {
        var detailsOwnsCompactRightPlane = ClientSize.Width <= 900 && DetailsPanel.IsVisible;
        FocusHud.IsVisible = !detailsOwnsCompactRightPlane;
        ModeHud.IsVisible = !detailsOwnsCompactRightPlane;
        UtilityHud.IsVisible = !detailsOwnsCompactRightPlane;
    }

    private void UpdateHistoryView()
    {
        if (HistoryText is not null)
        {
            HistoryText.Text = _session.NavigationTrailSummary;
        }
    }

    private void AnnounceStatus(string message)
    {
        if (string.Equals(_lastAnnouncement, message, StringComparison.Ordinal))
        {
            return;
        }

        _lastAnnouncement = message;
        StatusAnnouncer.Text = message;
        AutomationProperties.SetName(StatusAnnouncer, message);
    }

    private void UpdateMotionActivity()
    {
        var active = IsVisible && IsActive && WindowState != WindowState.Minimized;
        GraphScene.SetMotionActivity(active);
        DataRain.SetMotionActivity(active);
    }

    private void SetTransientStatus(string message)
    {
        StatusText.Text = message;
        AnnounceStatus(message);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _diagnosticsTimer.Stop();
        _voiceVisualTimer.Stop();
        _voiceTranscriptTimer.Stop();
        _detailsTypingTimer.Stop();
        GraphScene.SetMotionActivity(false);
        DataRain.SetMotionActivity(false);
        GraphScene.NodeHovered -= OnGraphNodeHovered;
        _voice.StateChanged -= OnVoiceStateChanged;
        _voice.Dispose();
        _sound.Dispose();
        _connection.StateChanged -= OnConnectionStateChanged;
        _session.ProviderFailed -= OnProviderFailed;
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

    private void ApplyContextFilterToControls()
    {
        _isApplyingContextFilters = true;
        try
        {
            var filter = _session.ContextFilter;
            ContextKindFilter.SelectedIndex = IndexFromKind(filter.Kind);
            ContextStrengthFilter.SelectedIndex = IndexFromStrength(filter.MinimumStrength);
            ContextEvidenceFilter.SelectedIndex = IndexFromEvidence(filter.EvidenceClass);
        }
        finally
        {
            _isApplyingContextFilters = false;
        }

        var summary = _session.ContextFilterSummary;
        ContextFilterSummaryText.Text = summary is null
            ? "No authoritative Context response is loaded."
            : $"{summary.VisibleRelationshipCount:N0} visible · {summary.MatchingRelationshipCount:N0} matching · {summary.AuthoritativeRelationshipCount:N0} authorized";
        ClearContextFiltersButton.IsEnabled = _session.ContextFilter.IsActive;
    }

    private static ExplorerRelationshipKind? KindFromIndex(int index) => index switch
    {
        1 => ExplorerRelationshipKind.Related,
        2 => ExplorerRelationshipKind.Topic,
        3 => ExplorerRelationshipKind.Entity,
        4 => ExplorerRelationshipKind.Temporal,
        5 => ExplorerRelationshipKind.Ocr,
        6 => ExplorerRelationshipKind.Transcript,
        _ => null,
    };

    private static int IndexFromKind(ExplorerRelationshipKind? kind) => kind switch
    {
        ExplorerRelationshipKind.Related => 1,
        ExplorerRelationshipKind.Topic => 2,
        ExplorerRelationshipKind.Entity => 3,
        ExplorerRelationshipKind.Temporal => 4,
        ExplorerRelationshipKind.Ocr => 5,
        ExplorerRelationshipKind.Transcript => 6,
        _ => 0,
    };

    private static int StrengthFromIndex(int index) => index switch
    {
        1 => 60,
        2 => 80,
        3 => 100,
        _ => 0,
    };

    private static int IndexFromStrength(int strength) => strength switch
    {
        >= 100 => 3,
        >= 80 => 2,
        >= 60 => 1,
        _ => 0,
    };

    private static ExplorerRelationshipEvidenceClass? EvidenceFromIndex(int index) => index switch
    {
        1 => ExplorerRelationshipEvidenceClass.Deterministic,
        2 => ExplorerRelationshipEvidenceClass.Derived,
        _ => null,
    };

    private static int IndexFromEvidence(ExplorerRelationshipEvidenceClass? evidenceClass) => evidenceClass switch
    {
        ExplorerRelationshipEvidenceClass.Deterministic => 1,
        ExplorerRelationshipEvidenceClass.Derived => 2,
        _ => 0,
    };

    private static bool IsContextualMode(ExplorerViewMode viewMode) =>
        viewMode is ExplorerViewMode.Context or ExplorerViewMode.Hybrid;

    private static string DescribeRoles(ExplorerNode node)
    {
        var structural = (node.Roles & ExplorerNodeRole.Structural) != 0;
        var contextual = (node.Roles & ExplorerNodeRole.Contextual) != 0;
        return (structural, contextual) switch
        {
            (true, true) => "Structure and Context",
            (false, true) => "Context",
            _ => "Structure",
        };
    }

    private static string StrengthLabel(int strength) => strength switch
    {
        >= 100 => "Confirmed",
        >= 80 => "Strong",
        >= 60 => "Moderate",
        _ => "Limited",
    };

    private static string FriendlyProvenance(string provenance)
    {
        if (provenance.StartsWith("deterministic-evidence ", StringComparison.OrdinalIgnoreCase))
        {
            return $"OmniSorSe deterministic evidence · {provenance}";
        }

        return provenance switch
        {
            "Content Intelligence 1" => "Content Intelligence · Content Intelligence 1",
            "Media Intelligence 1" => "Media Intelligence · Media Intelligence 1",
            _ => provenance,
        };
    }

    private static bool IsConnectionFailure(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        TimeoutException or
        ExplorerProtocolException or
        ExplorerProtocolMalformedResponseException;

    private sealed record ConnectedRootItem(Protocol.ExplorerNode Node)
    {
        public override string ToString() => Node.AuthorizedPath ?? Node.Name;
    }

    private sealed record AccessibleNodeItem(
        string NodeId,
        string Name,
        string Description,
        string StateText,
        string AccessibleName)
    {
        public override string ToString() => AccessibleName;
    }
}
