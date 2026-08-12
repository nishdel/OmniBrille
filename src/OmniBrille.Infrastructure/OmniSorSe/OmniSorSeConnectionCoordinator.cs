using OmniSorSe.ExplorerProtocol;

namespace OmniBrille.Infrastructure.OmniSorSe;

public sealed class OmniSorSeConnectionCoordinator : IOmniSorSeConnectionCoordinator
{
    private readonly IExplorerProtocolClientFactory _clientFactory;
    private readonly IOmniSorSeSessionGrantReceiver _grantReceiver;
    private OmniSorSeSessionGrant? _grant;
    private int _reconnectCount;
    private string? _lastFailureCategory;

    public OmniSorSeConnectionCoordinator(
        IExplorerProtocolClientFactory? clientFactory = null,
        IOmniSorSeSessionGrantReceiver? grantReceiver = null)
    {
        _clientFactory = clientFactory ?? new NamedPipeExplorerProtocolClientFactory();
        _grantReceiver = grantReceiver ?? new NamedPipeSessionGrantReceiver();
    }

    public event EventHandler? StateChanged;

    public OmniSorSeConnectionState State { get; private set; } = OmniSorSeConnectionState.Standalone;

    public string UserStatus => State switch
    {
        OmniSorSeConnectionState.Standalone => "Standalone",
        OmniSorSeConnectionState.Discovering => "Waiting for OmniSorSe…",
        OmniSorSeConnectionState.Connecting => "Connecting…",
        OmniSorSeConnectionState.Connected => "Connected · OmniSorSe",
        OmniSorSeConnectionState.Disconnected => "OmniSorSe disconnected",
        OmniSorSeConnectionState.Unavailable => "OmniSorSe unavailable",
        OmniSorSeConnectionState.Incompatible => "Update required",
        OmniSorSeConnectionState.Reconnecting => "Reconnecting…",
        _ => "OmniSorSe connection error",
    };

    public ExplorerProtocolInfo? ProtocolInfo { get; private set; }

    public IReadOnlyList<ExplorerNode> AccessibleRoots { get; private set; } = [];

    public IExplorerProtocolClient? Client { get; private set; }

    public OmniSorSeConnectionDiagnostics Diagnostics
    {
        get
        {
            var client = Client?.Diagnostics;
            return new OmniSorSeConnectionDiagnostics(
                State,
                client?.Transport ?? _grant?.Transport ?? "none",
                ProtocolInfo is null ? "—" : $"{ProtocolInfo.ProtocolMajor}.{ProtocolInfo.ProtocolMinor}",
                client?.LastRequestDuration ?? TimeSpan.Zero,
                client?.LastResponseNodeCount ?? 0,
                client?.LastSearchResultCount ?? 0,
                client?.TimeoutCount ?? 0,
                _reconnectCount,
                client?.StaleResponseRejectionCount ?? 0,
                _lastFailureCategory ?? client?.LastFailureCategory);
        }
    }

    public async Task<bool> ConnectFromHandoffAsync(
        string handoffEndpoint,
        CancellationToken cancellationToken = default)
    {
        SetState(OmniSorSeConnectionState.Discovering);
        try
        {
            var grant = await _grantReceiver.ReceiveAsync(handoffEndpoint, cancellationToken).ConfigureAwait(false);
            return await ConnectAsync(grant, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            SetFailure(OmniSorSeConnectionState.Unavailable, "Cancelled");
            throw;
        }
        catch (ExplorerProtocolTimeoutException)
        {
            SetFailure(OmniSorSeConnectionState.Unavailable, "Handoff timeout");
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          ExplorerProtocolMalformedResponseException or ArgumentException)
        {
            SetFailure(OmniSorSeConnectionState.Error, exception.GetType().Name);
            return false;
        }
    }

    public async Task<bool> ConnectAsync(
        OmniSorSeSessionGrant grant,
        CancellationToken cancellationToken = default)
    {
        _grant = grant;
        SetState(OmniSorSeConnectionState.Connecting);
        try
        {
            ExplorerProtocolValidation.ValidateGrant(grant, DateTimeOffset.UtcNow);
            var client = _clientFactory.Create(grant);
            var info = await client.GetProtocolInfoAsync(cancellationToken).ConfigureAwait(false);
            ExplorerProtocolValidation.ValidateProtocolInfo(info);
            var roots = await client.GetAccessibleRootsAsync(cancellationToken).ConfigureAwait(false);
            if (roots.Nodes.Count == 0)
            {
                Client = client;
                ProtocolInfo = info;
                AccessibleRoots = [];
                SetFailure(OmniSorSeConnectionState.Error, "Empty authorized root scope");
                return false;
            }

            if (roots.Nodes.Any(node =>
                    node.Kind != ExplorerNodeKind.Source ||
                    node.ParentId is not null))
            {
                throw new ExplorerProtocolMalformedResponseException(
                    "OmniSorSe returned an invalid accessible-root projection.");
            }

            Client = client;
            ProtocolInfo = info;
            AccessibleRoots = roots.Nodes.ToArray();
            _lastFailureCategory = null;
            SetState(OmniSorSeConnectionState.Connected);
            return true;
        }
        catch (OperationCanceledException)
        {
            SetFailure(OmniSorSeConnectionState.Disconnected, "Cancelled");
            throw;
        }
        catch (ExplorerProtocolException exception) when (exception.Code == ExplorerErrorCode.UnsupportedProtocol)
        {
            SetFailure(OmniSorSeConnectionState.Incompatible, exception.Code.ToString());
            return false;
        }
        catch (ExplorerProtocolException exception) when (exception.Code == ExplorerErrorCode.SessionExpired)
        {
            SetFailure(OmniSorSeConnectionState.Unavailable, exception.Code.ToString());
            return false;
        }
        catch (Exception exception) when (exception is ExplorerProtocolException or ExplorerProtocolTimeoutException or
                                          ExplorerProtocolMalformedResponseException or IOException or UnauthorizedAccessException)
        {
            SetFailure(OmniSorSeConnectionState.Disconnected, exception.GetType().Name);
            return false;
        }
    }

    public async Task<bool> RetryAsync(CancellationToken cancellationToken = default)
    {
        if (_grant is null || _grant.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            SetFailure(OmniSorSeConnectionState.Unavailable, "No current session grant");
            return false;
        }

        _reconnectCount++;
        SetState(OmniSorSeConnectionState.Reconnecting);
        return await ConnectAsync(_grant, cancellationToken).ConfigureAwait(false);
    }

    public void UseStandalone()
    {
        Client = null;
        ProtocolInfo = null;
        AccessibleRoots = [];
        SetState(OmniSorSeConnectionState.Standalone);
    }

    public void ReportDisconnected(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        SetFailure(OmniSorSeConnectionState.Disconnected, exception.GetType().Name);
    }

    private void SetFailure(OmniSorSeConnectionState state, string category)
    {
        _lastFailureCategory = category;
        SetState(state);
    }

    private void SetState(OmniSorSeConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
