using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

namespace OmniBrille.Infrastructure.OmniSorSe;

public sealed class NamedPipeSessionGrantReceiver : IOmniSorSeSessionGrantReceiver
{
    private const string HandoffPrefix = "omnibrille-handoff-";
    private const int MaximumGrantBytes = 4096;
    private static readonly TimeSpan DefaultHandoffTimeout = TimeSpan.FromSeconds(15);
    private readonly JsonSerializerOptions _json = ExplorerProtocolSerialization.CreateOptions();
    private readonly TimeSpan _handoffTimeout;

    public NamedPipeSessionGrantReceiver(TimeSpan? handoffTimeout = null)
    {
        _handoffTimeout = handoffTimeout ?? DefaultHandoffTimeout;
        if (_handoffTimeout <= TimeSpan.Zero || _handoffTimeout > DefaultHandoffTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(handoffTimeout));
        }
    }

    public async Task<OmniSorSeSessionGrant> ReceiveAsync(
        string handoffEndpoint,
        CancellationToken cancellationToken)
    {
        if (!IsValidHandoffEndpoint(handoffEndpoint))
        {
            throw new ArgumentException("The OmniSorSe handoff endpoint is invalid.", nameof(handoffEndpoint));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_handoffTimeout);
        using var pipe = new NamedPipeClientStream(
            ".",
            handoffEndpoint,
            PipeDirection.In,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            var lengthBytes = new byte[sizeof(int)];
            await pipe.ReadExactlyAsync(lengthBytes, timeout.Token).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            if (length is <= 0 or > MaximumGrantBytes)
            {
                throw new ExplorerProtocolMalformedResponseException("The OmniSorSe handoff frame is invalid.");
            }

            var bytes = new byte[length];
            await pipe.ReadExactlyAsync(bytes, timeout.Token).ConfigureAwait(false);
            var grant = JsonSerializer.Deserialize<OmniSorSeSessionGrant>(bytes, _json) ??
                throw new ExplorerProtocolMalformedResponseException("The OmniSorSe handoff did not contain a session grant.");
            ExplorerProtocolValidation.ValidateGrant(grant, DateTimeOffset.UtcNow);
            return grant;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExplorerProtocolTimeoutException("The one-time OmniSorSe session handoff timed out.", exception);
        }
        catch (JsonException exception)
        {
            throw new ExplorerProtocolMalformedResponseException(
                $"The OmniSorSe handoff did not match the strict bootstrap contract: {exception.Message}");
        }
    }

    private static bool IsValidHandoffEndpoint(string? handoffEndpoint) =>
        handoffEndpoint is not null &&
        handoffEndpoint.Length == HandoffPrefix.Length + 32 &&
        handoffEndpoint.StartsWith(HandoffPrefix, StringComparison.Ordinal) &&
        handoffEndpoint[HandoffPrefix.Length..].All(Uri.IsHexDigit);
}
