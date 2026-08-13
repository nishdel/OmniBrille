using OmniSorSe.ExplorerProtocol;

namespace OmniBrille.Infrastructure.OmniSorSe;

public static class ExplorerProtocolValidation
{
    public const int MaximumRequestBytes = 64 * 1024;
    public const int MaximumResponseBytes = 1024 * 1024;
    public const int MaximumNodeIdCharacters = 80;
    public const int MaximumNodeNameCharacters = 256;
    public const int MaximumNodes = 256;
    public const int MaximumEdges = 512;
    public const int MaximumSearchResults = 100;
    public const int MaximumRelatedResults = 100;

    public static void ValidateGrant(OmniSorSeSessionGrant grant, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (!string.Equals(grant.Transport, "named-pipe", StringComparison.Ordinal) ||
            grant.Endpoint.Length != 36 ||
            !grant.Endpoint.StartsWith("ose-", StringComparison.Ordinal) ||
            grant.Endpoint[4..].Any(character => !Uri.IsHexDigit(character)) ||
            string.IsNullOrWhiteSpace(grant.SessionId) || grant.SessionId.Length > 64 ||
            string.IsNullOrWhiteSpace(grant.AuthorizationToken) || grant.AuthorizationToken.Length > 128 ||
            grant.SessionId.Any(char.IsControl) || grant.AuthorizationToken.Any(char.IsControl))
        {
            throw new ExplorerProtocolMalformedResponseException("The OmniSorSe session grant is invalid.");
        }

        if (grant.ProtocolMajor != ExplorerProtocolVersion.Major)
        {
            throw new ExplorerProtocolException(
                ExplorerErrorCode.UnsupportedProtocol,
                $"Explorer Protocol {ExplorerProtocolVersion.Major} is required; the grant advertises {grant.ProtocolMajor}.");
        }

        if (grant.ExpiresAtUtc <= now)
        {
            throw new ExplorerProtocolException(ExplorerErrorCode.SessionExpired, "The OmniSorSe session grant has expired.");
        }
    }

    public static void ValidateProtocolInfo(ExplorerProtocolInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.ProtocolMajor != ExplorerProtocolVersion.Major)
        {
            throw new ExplorerProtocolException(
                ExplorerErrorCode.UnsupportedProtocol,
                $"Explorer Protocol {ExplorerProtocolVersion.Major} is required; OmniSorSe reports {info.ProtocolMajor}.");
        }

        var required = ExplorerCapability.Structure | ExplorerCapability.Search;
        if (!string.Equals(info.ApplicationName, "OmniSorSe", StringComparison.Ordinal) ||
            !info.IsReadOnly ||
            (info.Capabilities & required) != required)
        {
            throw new ExplorerProtocolMalformedResponseException("The server does not expose the required read-only OmniSorSe Structure and Search capabilities.");
        }

        var limits = info.Limits;
        if (limits.MaximumRequestBytes is <= 0 or > MaximumRequestBytes ||
            limits.MaximumResponseBytes is <= 0 or > MaximumResponseBytes ||
            limits.MaximumNodes is <= 0 or > MaximumNodes ||
            limits.MaximumEdges is <= 0 or > MaximumEdges ||
            limits.MaximumSearchResults is <= 0 or > MaximumSearchResults ||
            limits.MaximumRelatedResults is <= 0 or > MaximumRelatedResults ||
            limits.MaximumQueryCharacters <= 0 ||
            limits.RequestTimeoutSeconds is <= 0 or > 60)
        {
            throw new ExplorerProtocolMalformedResponseException("OmniSorSe advertised invalid or unsafe Explorer Protocol limits.");
        }
    }

    public static void ValidateNodePage(ExplorerNodePage page, int maximumNodes)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.Nodes.Count > maximumNodes || page.TotalAvailable < page.Nodes.Count || page.TotalAvailable < 0)
        {
            throw new ExplorerProtocolMalformedResponseException("The Explorer node page exceeds negotiated bounds.");
        }

        ValidateNodes(page.Nodes);
        ValidateContinuation(page.ContinuationToken);
    }

    public static void ValidateNeighborhood(ExplorerNeighborhood neighborhood, int maximumNodes, int maximumEdges)
    {
        ArgumentNullException.ThrowIfNull(neighborhood);
        if (neighborhood.Nodes.Count > maximumNodes || neighborhood.Edges.Count > maximumEdges)
        {
            throw new ExplorerProtocolMalformedResponseException("The Explorer neighborhood exceeds negotiated bounds.");
        }

        ValidateIdentifier(neighborhood.FocusNodeId, "focus node");
        ValidateNodes(neighborhood.Nodes);
        var ids = neighborhood.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        if (!ids.Contains(neighborhood.FocusNodeId) || neighborhood.Edges.Any(edge =>
                !ids.Contains(edge.SourceId) || !ids.Contains(edge.TargetId) ||
                edge.Strength is < 0 or > 100 || edge.Reason.Length > 256 || edge.Provenance.Length > 128))
        {
            throw new ExplorerProtocolMalformedResponseException("The Explorer neighborhood contains invalid node or edge references.");
        }

        ValidateContinuation(neighborhood.ContinuationToken);
    }

    public static void ValidateSearch(ExplorerSearchResult result, int maximumResults)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Results.Count > maximumResults || result.Results.Any(hit =>
                hit.Rank <= 0 || !double.IsFinite(hit.Score) || hit.Explanation.Length > 256))
        {
            throw new ExplorerProtocolMalformedResponseException("The Explorer Search response exceeds negotiated bounds.");
        }

        ValidateNodes(result.Results.Select(hit => hit.Node));
    }

    public static void ValidateRelated(
        ExplorerRelatedResult result,
        int maximumResults,
        string focusNodeId)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateIdentifier(focusNodeId, "Related focus node");
        if (result.Nodes.Count > maximumResults || result.Edges.Count > maximumResults)
        {
            throw new ExplorerProtocolMalformedResponseException("The Explorer Related response exceeds negotiated bounds.");
        }

        ValidateNodes(result.Nodes);
        var ids = result.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var edge in result.Edges)
        {
            ValidateIdentifier(edge.SourceId, "relationship source");
            ValidateIdentifier(edge.TargetId, "relationship target");
            if (!StringComparer.Ordinal.Equals(edge.SourceId, focusNodeId) ||
                !ids.Contains(edge.TargetId) ||
                edge.Kind == ExplorerEdgeKind.Contains ||
                edge.Strength is < 0 or > 100 ||
                edge.Reason.Length > 256 ||
                edge.Provenance.Length > 128)
            {
                throw new ExplorerProtocolMalformedResponseException("The Explorer Related response contains an invalid relationship.");
            }
        }
    }

    public static void ValidateDetails(ExplorerNodeDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        ValidateNodes([details.Node]);
        if (details.Summary?.Length > 512 || details.Topics.Count > 32 || details.Entities.Count > 32 ||
            details.RelationshipSummaries.Count > 8)
        {
            throw new ExplorerProtocolMalformedResponseException("The Explorer details response exceeds client bounds.");
        }
    }

    private static void ValidateNodes(IEnumerable<ExplorerNode> nodes)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            ValidateIdentifier(node.Id, "node");
            if (!identifiers.Add(node.Id) || string.IsNullOrWhiteSpace(node.Name) ||
                node.Name.Length > MaximumNodeNameCharacters || node.Name.Any(char.IsControl) ||
                node.ChildCount < 0 || node.RelationshipCount < 0 || node.Metadata.Count > 32)
            {
                throw new ExplorerProtocolMalformedResponseException("The Explorer response contains an invalid node.");
            }

            if (node.ParentId is not null)
            {
                ValidateIdentifier(node.ParentId, "parent node");
            }
        }
    }

    private static void ValidateIdentifier(string id, string field)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > MaximumNodeIdCharacters || id.Any(char.IsControl))
        {
            throw new ExplorerProtocolMalformedResponseException($"The Explorer {field} identifier is invalid.");
        }
    }

    private static void ValidateContinuation(string? continuation)
    {
        if (continuation is not null && (continuation.Length > 24 || continuation.Any(char.IsControl)))
        {
            throw new ExplorerProtocolMalformedResponseException("The Explorer continuation token is invalid.");
        }
    }
}
