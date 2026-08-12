using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using OmniBrille.Infrastructure.OmniSorSe;
using OmniSorSe.ExplorerProtocol;

namespace OmniBrille.Tests;

public sealed class NamedPipeExplorerProtocolClientTests
{
    [Fact]
    public async Task Client_UsesProductionLengthPrefixedV1Envelope()
    {
        var endpoint = NewEndpoint();
        var grant = Grant(endpoint);
        ExplorerRequestEnvelope? observed = null;
        var server = ServeOnceAsync(endpoint, request =>
        {
            observed = request;
            return Success(request, Info());
        });
        var client = new NamedPipeExplorerProtocolClient(grant);

        var info = await client.GetProtocolInfoAsync(CancellationToken.None);
        await server;

        Assert.Equal(ExplorerOperation.GetProtocolInfo, observed!.Operation);
        Assert.Equal(grant.SessionId, observed.SessionId);
        Assert.Equal(grant.AuthorizationToken, observed.AuthorizationToken);
        Assert.Equal("OmniSorSe", info.ApplicationName);
    }

    [Fact]
    public async Task Client_MapsStableProtocolError()
    {
        var endpoint = NewEndpoint();
        var server = ServeOnceAsync(endpoint, request => new ExplorerResponseEnvelope(
            1,
            request.RequestId,
            false,
            null,
            new ExplorerProtocolError(ExplorerErrorCode.NodeNotFound, "The node is unavailable.", false)));
        var client = new NamedPipeExplorerProtocolClient(Grant(endpoint));

        var exception = await Assert.ThrowsAsync<ExplorerProtocolException>(() =>
            client.GetNodeDetailsAsync(new ExplorerNodeDetailsRequest("missing"), CancellationToken.None));
        await server;

        Assert.Equal(ExplorerErrorCode.NodeNotFound, exception.Code);
        Assert.False(exception.Retryable);
    }

    [Fact]
    public async Task Client_RejectsMismatchedResponseIdentity()
    {
        var endpoint = NewEndpoint();
        var server = ServeOnceAsync(endpoint, request => new ExplorerResponseEnvelope(
            1,
            "different-request",
            true,
            JsonSerializer.SerializeToElement(Info(), ExplorerProtocolSerialization.CreateOptions()),
            null));
        var client = new NamedPipeExplorerProtocolClient(Grant(endpoint));

        await Assert.ThrowsAsync<ExplorerProtocolMalformedResponseException>(() =>
            client.GetProtocolInfoAsync(CancellationToken.None));
        await server;
    }

    [Fact]
    public async Task HandoffReceiver_ReadsBoundedOneTimeGrantWithoutCommandLineSecret()
    {
        var handoff = $"omnibrille-handoff-{Guid.NewGuid():N}";
        var grant = Grant(NewEndpoint());
        var server = Task.Run(async () =>
        {
            await using var pipe = new NamedPipeServerStream(
                handoff,
                PipeDirection.Out,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(grant, ExplorerProtocolSerialization.CreateOptions());
            var length = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            await pipe.WriteAsync(length);
            await pipe.WriteAsync(bytes);
            await pipe.FlushAsync();
        });
        var receiver = new NamedPipeSessionGrantReceiver();

        var received = await receiver.ReceiveAsync(handoff, CancellationToken.None);
        await server;

        Assert.Equal(grant, received);
    }

    private static async Task ServeOnceAsync(
        string endpoint,
        Func<ExplorerRequestEnvelope, ExplorerResponseEnvelope> responseFactory)
    {
        await using var pipe = new NamedPipeServerStream(
            endpoint,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.WaitForConnectionAsync();
        var length = new byte[sizeof(int)];
        await pipe.ReadExactlyAsync(length);
        var requestBytes = new byte[BinaryPrimitives.ReadInt32LittleEndian(length)];
        await pipe.ReadExactlyAsync(requestBytes);
        var options = ExplorerProtocolSerialization.CreateOptions();
        var request = JsonSerializer.Deserialize<ExplorerRequestEnvelope>(requestBytes, options)!;
        var responseBytes = JsonSerializer.SerializeToUtf8Bytes(responseFactory(request), options);
        BinaryPrimitives.WriteInt32LittleEndian(length, responseBytes.Length);
        await pipe.WriteAsync(length);
        await pipe.WriteAsync(responseBytes);
        await pipe.FlushAsync();
    }

    private static ExplorerResponseEnvelope Success(ExplorerRequestEnvelope request, object payload) => new(
        1,
        request.RequestId,
        true,
        JsonSerializer.SerializeToElement(payload, payload.GetType(), ExplorerProtocolSerialization.CreateOptions()),
        null);

    private static ExplorerProtocolInfo Info() => new(
        1,
        0,
        "OmniSorSe",
        "2.4.0",
        ExplorerCapability.Structure | ExplorerCapability.Search,
        new ExplorerProtocolLimits(65536, 1048576, 500, 256, 512, 100, 100, 2, 320, 32, 32, 256, 4, 15),
        true,
        "Local named pipe (Unix-domain-backed on Unix hosts)");

    private static string NewEndpoint() => "ose-" + Guid.NewGuid().ToString("N");

    private static OmniSorSeSessionGrant Grant(string endpoint) => new(
        "named-pipe",
        endpoint,
        "session-id",
        "authorization-secret",
        DateTimeOffset.UtcNow.AddMinutes(2),
        1,
        0);
}
