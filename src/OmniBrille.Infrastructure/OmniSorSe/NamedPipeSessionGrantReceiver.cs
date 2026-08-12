using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

namespace OmniBrille.Infrastructure.OmniSorSe;

public sealed class NamedPipeSessionGrantReceiver : IOmniSorSeSessionGrantReceiver
{
    private const int MaximumGrantBytes = 4096;
    private static readonly TimeSpan HandoffTimeout = TimeSpan.FromSeconds(10);
    private readonly JsonSerializerOptions _json = ExplorerProtocolSerialization.CreateOptions();

    public async Task<OmniSorSeSessionGrant> ReceiveAsync(
        string handoffEndpoint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(handoffEndpoint) || handoffEndpoint.Length > 128 ||
            handoffEndpoint.Any(char.IsControl))
        {
            throw new ArgumentException("The OmniSorSe handoff endpoint is invalid.", nameof(handoffEndpoint));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HandoffTimeout);
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
    }
}
