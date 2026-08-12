using System.Text.Json;
using OmniBrille.Infrastructure.OmniSorSe;
using OmniSorSe.ExplorerProtocol;

namespace OmniBrille.Tests;

public sealed class ExplorerProtocolSerializationTests
{
    [Fact]
    public void Contract_ParsesValidOptionalFieldsAndStringEnums()
    {
        const string json = """
            {
              "id":"node-1",
              "name":"Readme",
              "kind":"File",
              "parentId":null,
              "extension":".md",
              "sizeBytes":42,
              "authorizedPath":null,
              "metadata":{},
              "childCount":0,
              "relationshipCount":0
            }
            """;

        var node = JsonSerializer.Deserialize<ExplorerNode>(json, ExplorerProtocolSerialization.CreateOptions());

        Assert.NotNull(node);
        Assert.Equal(ExplorerNodeKind.File, node.Kind);
        Assert.Null(node.AuthorizedPath);
    }

    [Fact]
    public void Contract_RejectsUnknownMembers()
    {
        const string json = """
            {
              "id":"node-1","name":"Readme","kind":"File","parentId":null,
              "extension":null,"sizeBytes":null,"authorizedPath":null,"metadata":{},
              "childCount":0,"relationshipCount":0,"unexpected":true
            }
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ExplorerNode>(json, ExplorerProtocolSerialization.CreateOptions()));
    }

    [Fact]
    public void Contract_RejectsNumericOrUnknownEnums()
    {
        var options = ExplorerProtocolSerialization.CreateOptions();
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ExplorerNode>(NodeJson("999"), options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ExplorerNode>(NodeJson("\"FutureKind\""), options));
    }

    [Fact]
    public void GrantValidation_RejectsWrongVersionExpiredAndMalformedEndpoint()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ExplorerProtocolException>(() =>
            ExplorerProtocolValidation.ValidateGrant(Grant(protocolMajor: 2), now));
        Assert.Throws<ExplorerProtocolException>(() =>
            ExplorerProtocolValidation.ValidateGrant(Grant(expiresAt: now.AddSeconds(-1)), now));
        Assert.Throws<ExplorerProtocolMalformedResponseException>(() =>
            ExplorerProtocolValidation.ValidateGrant(Grant(endpoint: "discoverable-fixed-name"), now));
    }

    [Fact]
    public void PayloadValidation_RejectsDuplicateIdsAndOversizedCollections()
    {
        var node = Node("same");
        Assert.Throws<ExplorerProtocolMalformedResponseException>(() =>
            ExplorerProtocolValidation.ValidateNodePage(
                new ExplorerNodePage([node, node], 2, false, null),
                256));
        Assert.Throws<ExplorerProtocolMalformedResponseException>(() =>
            ExplorerProtocolValidation.ValidateSearch(
                new ExplorerSearchResult(
                    Enumerable.Range(0, 101).Select(index =>
                        new ExplorerSearchHit(Node($"n-{index}"), index + 1, 1, "match", null, null)).ToArray(),
                    false,
                    "coverage",
                    false),
                100));
    }

    private static string NodeJson(string kind) => $$"""
        {"id":"node-1","name":"Readme","kind":{{kind}},"parentId":null,
        "extension":null,"sizeBytes":null,"authorizedPath":null,"metadata":{},
        "childCount":0,"relationshipCount":0}
        """;

    private static ExplorerNode Node(string id) =>
        new(id, id, ExplorerNodeKind.File, null, null, null, null, new Dictionary<string, string>(), 0, 0);

    private static OmniSorSeSessionGrant Grant(
        int protocolMajor = 1,
        string endpoint = "ose-0123456789abcdef0123456789abcdef",
        DateTimeOffset? expiresAt = null) =>
        new("named-pipe", endpoint, "session", "secret", expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(1), protocolMajor, 0);
}
