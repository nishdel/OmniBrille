using OmniSorSe.ExplorerProtocol;

namespace OmniBrille.Infrastructure.OmniSorSe;

public enum OmniSorSeConnectionState
{
    Standalone,
    Discovering,
    Connecting,
    Connected,
    Disconnected,
    Unavailable,
    Incompatible,
    Error,
    Reconnecting,
}

public sealed record OmniSorSeSessionGrant(
    string Transport,
    string Endpoint,
    string SessionId,
    string AuthorizationToken,
    DateTimeOffset ExpiresAtUtc,
    int ProtocolMajor,
    int ProtocolMinor);

public sealed record OmniSorSeConnectionDiagnostics(
    OmniSorSeConnectionState State,
    string Transport,
    string ProtocolVersion,
    TimeSpan LastRequestDuration,
    int LastResponseNodeCount,
    int LastSearchResultCount,
    int TimeoutCount,
    int ReconnectCount,
    int StaleResponseRejectionCount,
    string? LastFailureCategory);

public sealed class ExplorerProtocolException : Exception
{
    public ExplorerProtocolException(ExplorerErrorCode code, string message, bool retryable = false)
        : base(message)
    {
        Code = code;
        Retryable = retryable;
    }

    public ExplorerErrorCode Code { get; }

    public bool Retryable { get; }
}

public sealed class ExplorerProtocolTimeoutException : TimeoutException
{
    public ExplorerProtocolTimeoutException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class ExplorerProtocolMalformedResponseException : Exception
{
    public ExplorerProtocolMalformedResponseException(string message)
        : base(message)
    {
    }
}

public interface IExplorerProtocolClient
{
    public OmniSorSeSessionGrant Grant { get; }

    public OmniSorSeConnectionDiagnostics Diagnostics { get; }

    public Task<ExplorerProtocolInfo> GetProtocolInfoAsync(CancellationToken cancellationToken);

    public Task<ExplorerNodePage> GetAccessibleRootsAsync(CancellationToken cancellationToken);

    public Task<ExplorerNodePage> GetChildrenAsync(ExplorerChildrenRequest request, CancellationToken cancellationToken);

    public Task<ExplorerNeighborhood> GetNeighborhoodAsync(ExplorerNeighborhoodRequest request, CancellationToken cancellationToken);

    public Task<ExplorerRelatedResult> GetRelatedAsync(
        ExplorerRelatedRequest request,
        CancellationToken cancellationToken);

    public Task<ExplorerSearchResult> SearchAsync(ExplorerSearchRequest request, CancellationToken cancellationToken);

    public Task<ExplorerNodeDetails> GetNodeDetailsAsync(ExplorerNodeDetailsRequest request, CancellationToken cancellationToken);

    public void ReportStaleResponseRejected();
}

public interface IExplorerProtocolClientFactory
{
    public IExplorerProtocolClient Create(OmniSorSeSessionGrant grant);
}

public interface IOmniSorSeSessionGrantReceiver
{
    public Task<OmniSorSeSessionGrant> ReceiveAsync(string handoffEndpoint, CancellationToken cancellationToken);
}

public interface IOmniSorSeConnectionCoordinator
{
    public event EventHandler? StateChanged;

    public OmniSorSeConnectionState State { get; }

    public string UserStatus { get; }

    public ExplorerProtocolInfo? ProtocolInfo { get; }

    public IReadOnlyList<ExplorerNode> AccessibleRoots { get; }

    public IExplorerProtocolClient? Client { get; }

    public OmniSorSeConnectionDiagnostics Diagnostics { get; }

    public Task<bool> ConnectFromHandoffAsync(string handoffEndpoint, CancellationToken cancellationToken = default);

    public Task<bool> ConnectAsync(OmniSorSeSessionGrant grant, CancellationToken cancellationToken = default);

    public Task<bool> RetryAsync(CancellationToken cancellationToken = default);

    public void UseStandalone();

    public void ReportDisconnected(Exception exception);
}
