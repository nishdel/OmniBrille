using OmniBrille.Infrastructure.OmniSorSe;
using OmniSorSe.ExplorerProtocol;

namespace OmniBrille.Tests;

public sealed class OmniSorSeConnectionCoordinatorTests
{
    [Fact]
    public async Task Connect_ValidatesProtocolAndLoadsOnlyAuthorizedRoots()
    {
        var root = Node("authorized-root", ExplorerNodeKind.Source);
        var client = new FakeClient { Roots = new ExplorerNodePage([root], 1, false, null) };
        var coordinator = new OmniSorSeConnectionCoordinator(new FixedFactory(client), new FixedReceiver(client.Grant));

        var connected = await coordinator.ConnectFromHandoffAsync("handoff");

        Assert.True(connected);
        Assert.Equal(OmniSorSeConnectionState.Connected, coordinator.State);
        Assert.Equal("Connected · OmniSorSe", coordinator.UserStatus);
        Assert.Equal(root.Id, Assert.Single(coordinator.AccessibleRoots).Id);
    }

    [Fact]
    public async Task Connect_IncompatibleMajorLeavesStandaloneAvailable()
    {
        var client = new FakeClient
        {
            Info = Info() with { ProtocolMajor = 2 },
            Roots = new ExplorerNodePage([Node("root", ExplorerNodeKind.Source)], 1, false, null),
        };
        var coordinator = new OmniSorSeConnectionCoordinator(new FixedFactory(client), new FixedReceiver(client.Grant));

        var connected = await coordinator.ConnectAsync(client.Grant);

        Assert.False(connected);
        Assert.Equal(OmniSorSeConnectionState.Incompatible, coordinator.State);
        coordinator.UseStandalone();
        Assert.Equal(OmniSorSeConnectionState.Standalone, coordinator.State);
    }

    [Fact]
    public async Task Retry_IsConservativeAndRevalidatesCurrentGrant()
    {
        var client = new FakeClient
        {
            Roots = new ExplorerNodePage([Node("root", ExplorerNodeKind.Source)], 1, false, null),
        };
        var coordinator = new OmniSorSeConnectionCoordinator(new FixedFactory(client), new FixedReceiver(client.Grant));
        Assert.True(await coordinator.ConnectAsync(client.Grant));
        coordinator.ReportDisconnected(new IOException("controlled disconnect"));

        var reconnected = await coordinator.RetryAsync();

        Assert.True(reconnected);
        Assert.Equal(1, coordinator.Diagnostics.ReconnectCount);
        Assert.Equal(OmniSorSeConnectionState.Connected, coordinator.State);
    }

    [Fact]
    public async Task EmptyScope_FailsWithoutInventingFilesystemRoots()
    {
        var client = new FakeClient { Roots = new ExplorerNodePage([], 0, false, null) };
        var coordinator = new OmniSorSeConnectionCoordinator(new FixedFactory(client), new FixedReceiver(client.Grant));

        var connected = await coordinator.ConnectAsync(client.Grant);

        Assert.False(connected);
        Assert.Empty(coordinator.AccessibleRoots);
        Assert.Equal(OmniSorSeConnectionState.Error, coordinator.State);
    }

    [Fact]
    public async Task InvalidAccessibleRootProjection_FailsClosed()
    {
        var client = new FakeClient
        {
            Roots = new ExplorerNodePage([Node("not-a-root", ExplorerNodeKind.File)], 1, false, null),
        };
        var coordinator = new OmniSorSeConnectionCoordinator(new FixedFactory(client), new FixedReceiver(client.Grant));

        var connected = await coordinator.ConnectAsync(client.Grant);

        Assert.False(connected);
        Assert.Empty(coordinator.AccessibleRoots);
        Assert.Equal(OmniSorSeConnectionState.Disconnected, coordinator.State);
    }

    private static ExplorerNode Node(string id, ExplorerNodeKind kind) =>
        new(id, id, kind, null, null, null, null, new Dictionary<string, string>(), 0, 0);

    private static ExplorerProtocolInfo Info() => new(
        1, 0, "OmniSorSe", "2.4.0", ExplorerCapability.Structure | ExplorerCapability.Search,
        new ExplorerProtocolLimits(65536, 1048576, 500, 256, 512, 100, 100, 2, 320, 32, 32, 256, 4, 15),
        true, "Local named pipe");

    private sealed class FixedFactory(IExplorerProtocolClient client) : IExplorerProtocolClientFactory
    {
        public IExplorerProtocolClient Create(OmniSorSeSessionGrant grant) => client;
    }

    private sealed class FixedReceiver(OmniSorSeSessionGrant grant) : IOmniSorSeSessionGrantReceiver
    {
        public Task<OmniSorSeSessionGrant> ReceiveAsync(string handoffEndpoint, CancellationToken cancellationToken) =>
            Task.FromResult(grant);
    }

    private sealed class FakeClient : IExplorerProtocolClient
    {
        public OmniSorSeSessionGrant Grant { get; } = new(
            "named-pipe", "ose-0123456789abcdef0123456789abcdef", "session", "secret",
            DateTimeOffset.UtcNow.AddMinutes(2), 1, 0);
        public OmniSorSeConnectionDiagnostics Diagnostics { get; } = new(
            OmniSorSeConnectionState.Connected, "named-pipe", "1.0", TimeSpan.Zero, 0, 0, 0, 0, 0, null);
        public ExplorerProtocolInfo Info { get; init; } = OmniSorSeConnectionCoordinatorTests.Info();
        public ExplorerNodePage Roots { get; init; } = new([], 0, false, null);
        public Task<ExplorerProtocolInfo> GetProtocolInfoAsync(CancellationToken cancellationToken) => Task.FromResult(Info);
        public Task<ExplorerNodePage> GetAccessibleRootsAsync(CancellationToken cancellationToken) => Task.FromResult(Roots);
        public Task<ExplorerNodePage> GetChildrenAsync(ExplorerChildrenRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExplorerNeighborhood> GetNeighborhoodAsync(ExplorerNeighborhoodRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExplorerSearchResult> SearchAsync(ExplorerSearchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExplorerNodeDetails> GetNodeDetailsAsync(ExplorerNodeDetailsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void ReportStaleResponseRejected()
        {
        }
    }
}
