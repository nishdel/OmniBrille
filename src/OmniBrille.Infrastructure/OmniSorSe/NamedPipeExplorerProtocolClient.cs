using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using OmniSorSe.ExplorerProtocol;

namespace OmniBrille.Infrastructure.OmniSorSe;

public sealed class NamedPipeExplorerProtocolClientFactory : IExplorerProtocolClientFactory
{
    public IExplorerProtocolClient Create(OmniSorSeSessionGrant grant) =>
        new NamedPipeExplorerProtocolClient(grant);
}

public sealed class NamedPipeExplorerProtocolClient : IExplorerProtocolClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(18);
    private readonly JsonSerializerOptions _json = ExplorerProtocolSerialization.CreateOptions();
    private readonly object _diagnosticsLock = new();
    private OmniSorSeConnectionDiagnostics _diagnostics;

    public NamedPipeExplorerProtocolClient(OmniSorSeSessionGrant grant)
    {
        ExplorerProtocolValidation.ValidateGrant(grant, DateTimeOffset.UtcNow);
        Grant = grant;
        _diagnostics = new OmniSorSeConnectionDiagnostics(
            OmniSorSeConnectionState.Connecting,
            "named-pipe",
            $"{grant.ProtocolMajor}.{grant.ProtocolMinor}",
            TimeSpan.Zero,
            0,
            0,
            0,
            0,
            0,
            null);
    }

    public OmniSorSeSessionGrant Grant { get; }

    public OmniSorSeConnectionDiagnostics Diagnostics
    {
        get
        {
            lock (_diagnosticsLock)
            {
                return _diagnostics;
            }
        }
    }

    public async Task<ExplorerProtocolInfo> GetProtocolInfoAsync(CancellationToken cancellationToken)
    {
        var result = await SendAsync<object, ExplorerProtocolInfo>(
            ExplorerOperation.GetProtocolInfo,
            new { },
            cancellationToken).ConfigureAwait(false);
        ExplorerProtocolValidation.ValidateProtocolInfo(result);
        return result;
    }

    public async Task<ExplorerNodePage> GetAccessibleRootsAsync(CancellationToken cancellationToken)
    {
        var result = await SendAsync<object, ExplorerNodePage>(
            ExplorerOperation.GetAccessibleRoots,
            new { },
            cancellationToken).ConfigureAwait(false);
        ExplorerProtocolValidation.ValidateNodePage(result, 64);
        RecordNodes(result.Nodes.Count);
        return result;
    }

    public async Task<ExplorerNodePage> GetChildrenAsync(
        ExplorerChildrenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<ExplorerChildrenRequest, ExplorerNodePage>(
            ExplorerOperation.GetChildren,
            request,
            cancellationToken).ConfigureAwait(false);
        ExplorerProtocolValidation.ValidateNodePage(result, ExplorerProtocolValidation.MaximumNodes);
        RecordNodes(result.Nodes.Count);
        return result;
    }

    public async Task<ExplorerNeighborhood> GetNeighborhoodAsync(
        ExplorerNeighborhoodRequest request,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<ExplorerNeighborhoodRequest, ExplorerNeighborhood>(
            ExplorerOperation.GetNeighborhood,
            request,
            cancellationToken).ConfigureAwait(false);
        ExplorerProtocolValidation.ValidateNeighborhood(
            result,
            ExplorerProtocolValidation.MaximumNodes,
            ExplorerProtocolValidation.MaximumEdges);
        RecordNodes(result.Nodes.Count);
        return result;
    }

    public async Task<ExplorerRelatedResult> GetRelatedAsync(
        ExplorerRelatedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<ExplorerRelatedRequest, ExplorerRelatedResult>(
            ExplorerOperation.GetRelated,
            request,
            cancellationToken).ConfigureAwait(false);
        ExplorerProtocolValidation.ValidateRelated(
            result,
            Math.Min(request.MaximumResults ?? 50, ExplorerProtocolValidation.MaximumRelatedResults),
            request.NodeId);
        RecordNodes(result.Nodes.Count);
        return result;
    }

    public async Task<ExplorerSearchResult> SearchAsync(
        ExplorerSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<ExplorerSearchRequest, ExplorerSearchResult>(
            ExplorerOperation.Search,
            request,
            cancellationToken).ConfigureAwait(false);
        ExplorerProtocolValidation.ValidateSearch(result, ExplorerProtocolValidation.MaximumSearchResults);
        lock (_diagnosticsLock)
        {
            _diagnostics = _diagnostics with { LastSearchResultCount = result.Results.Count };
        }

        return result;
    }

    public async Task<ExplorerNodeDetails> GetNodeDetailsAsync(
        ExplorerNodeDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<ExplorerNodeDetailsRequest, ExplorerNodeDetails>(
            ExplorerOperation.GetNodeDetails,
            request,
            cancellationToken).ConfigureAwait(false);
        ExplorerProtocolValidation.ValidateDetails(result);
        return result;
    }

    public void ReportStaleResponseRejected()
    {
        lock (_diagnosticsLock)
        {
            _diagnostics = _diagnostics with
            {
                StaleResponseRejectionCount = _diagnostics.StaleResponseRejectionCount + 1,
            };
        }
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        ExplorerOperation operation,
        TRequest payload,
        CancellationToken cancellationToken)
        where TRequest : notnull
        where TResponse : class
    {
        ExplorerProtocolValidation.ValidateGrant(Grant, DateTimeOffset.UtcNow);
        var requestId = Guid.NewGuid().ToString("N");
        var request = new ExplorerRequestEnvelope(
            ExplorerProtocolVersion.Major,
            requestId,
            Grant.SessionId,
            Grant.AuthorizationToken,
            operation,
            JsonSerializer.SerializeToElement(payload, payload.GetType(), _json));
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, _json);
        if (requestBytes.Length is <= 0 or > ExplorerProtocolValidation.MaximumRequestBytes)
        {
            throw new ExplorerProtocolException(ExplorerErrorCode.RequestTooLarge, "The Explorer request exceeds the v1 frame limit.");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestCancellation.CancelAfter(RequestTimeout);
            using var pipe = new NamedPipeClientStream(
                ".",
                Grant.Endpoint,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using (var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation.Token))
            {
                connectCancellation.CancelAfter(ConnectTimeout);
                await pipe.ConnectAsync(connectCancellation.Token).ConfigureAwait(false);
            }

            var lengthBytes = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, requestBytes.Length);
            await pipe.WriteAsync(lengthBytes, requestCancellation.Token).ConfigureAwait(false);
            await pipe.WriteAsync(requestBytes, requestCancellation.Token).ConfigureAwait(false);
            await pipe.FlushAsync(requestCancellation.Token).ConfigureAwait(false);

            await pipe.ReadExactlyAsync(lengthBytes, requestCancellation.Token).ConfigureAwait(false);
            var responseLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            if (responseLength is <= 0 or > ExplorerProtocolValidation.MaximumResponseBytes)
            {
                throw new ExplorerProtocolMalformedResponseException("The Explorer response frame length is invalid.");
            }

            var responseBytes = new byte[responseLength];
            await pipe.ReadExactlyAsync(responseBytes, requestCancellation.Token).ConfigureAwait(false);
            var envelope = JsonSerializer.Deserialize<ExplorerResponseEnvelope>(responseBytes, _json) ??
                throw new ExplorerProtocolMalformedResponseException("The Explorer response envelope is missing.");
            if (!string.Equals(envelope.RequestId, requestId, StringComparison.Ordinal) ||
                envelope.ProtocolMajor != ExplorerProtocolVersion.Major)
            {
                throw new ExplorerProtocolMalformedResponseException("The Explorer response identity or major version does not match the request.");
            }

            if (!envelope.Success)
            {
                var error = envelope.Error ??
                    throw new ExplorerProtocolMalformedResponseException("The failed Explorer response omitted its error.");
                throw new ExplorerProtocolException(error.Code, error.Message, error.Retryable);
            }

            if (envelope.Error is not null || envelope.Payload is null)
            {
                throw new ExplorerProtocolMalformedResponseException("The successful Explorer response has an invalid envelope shape.");
            }

            return envelope.Payload.Value.Deserialize<TResponse>(_json) ??
                throw new ExplorerProtocolMalformedResponseException("The Explorer response payload is missing.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            lock (_diagnosticsLock)
            {
                _diagnostics = _diagnostics with
                {
                    TimeoutCount = _diagnostics.TimeoutCount + 1,
                    LastFailureCategory = "Timeout",
                };
            }

            throw new ExplorerProtocolTimeoutException("The local OmniSorSe Explorer request timed out.", exception);
        }
        catch (JsonException exception)
        {
            throw new ExplorerProtocolMalformedResponseException($"The Explorer response did not match Protocol v1: {exception.Message}");
        }
        finally
        {
            stopwatch.Stop();
            lock (_diagnosticsLock)
            {
                _diagnostics = _diagnostics with { LastRequestDuration = stopwatch.Elapsed };
            }
        }
    }

    private void RecordNodes(int count)
    {
        lock (_diagnosticsLock)
        {
            _diagnostics = _diagnostics with
            {
                State = OmniSorSeConnectionState.Connected,
                LastResponseNodeCount = count,
                LastFailureCategory = null,
            };
        }
    }
}
