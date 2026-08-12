using System.Text.Json;

namespace OmniSorSe.ExplorerProtocol;

public static class ExplorerProtocolVersion
{
    public const int Major = 1;
    public const int Minor = 0;
    public const string Display = "1.0";
}

public enum ExplorerOperation
{
    GetProtocolInfo,
    GetAccessibleRoots,
    GetChildren,
    GetNeighborhood,
    Search,
    GetRelated,
    GetNodeDetails,
}

[Flags]
public enum ExplorerCapability
{
    None = 0,
    Structure = 1 << 0,
    Search = 1 << 1,
    Context = 1 << 2,
    RelatedFiles = 1 << 3,
    MediaIntelligence = 1 << 4,
    ContentIntelligence = 1 << 5,
    Ocr = 1 << 6,
    Transcripts = 1 << 7,
    Topics = 1 << 8,
    Entities = 1 << 9,
    Summaries = 1 << 10,
}

public enum ExplorerNodeKind
{
    Source,
    Folder,
    File,
}

public enum ExplorerEdgeKind
{
    Contains,
    Related,
    Topic,
    Entity,
    Temporal,
    Ocr,
    Transcript,
}

public enum ExplorerEvidenceClass
{
    Structural,
    Deterministic,
    Derived,
}

public enum ExplorerErrorCode
{
    Unauthorized,
    SessionExpired,
    UnsupportedProtocol,
    CapabilityUnavailable,
    NodeNotFound,
    OutOfScope,
    RequestTooLarge,
    LimitExceeded,
    MalformedRequest,
    Cancelled,
    TemporarilyUnavailable,
    InternalFailure,
}

public sealed record ExplorerProtocolLimits(
    int MaximumRequestBytes,
    int MaximumResponseBytes,
    int MaximumQueryCharacters,
    int MaximumNodes,
    int MaximumEdges,
    int MaximumSearchResults,
    int MaximumRelatedResults,
    int MaximumDepth,
    int MaximumSnippetCharacters,
    int MaximumTopics,
    int MaximumEntities,
    int MaximumReasonCharacters,
    int MaximumConcurrentRequests,
    int RequestTimeoutSeconds);

public sealed record ExplorerProtocolInfo(
    int ProtocolMajor,
    int ProtocolMinor,
    string ApplicationName,
    string ApplicationVersion,
    ExplorerCapability Capabilities,
    ExplorerProtocolLimits Limits,
    bool IsReadOnly,
    string Transport);

public sealed record ExplorerNode(
    string Id,
    string Name,
    ExplorerNodeKind Kind,
    string? ParentId,
    string? Extension,
    long? SizeBytes,
    string? AuthorizedPath,
    IReadOnlyDictionary<string, string> Metadata,
    int ChildCount,
    int RelationshipCount);

public sealed record ExplorerEdge(
    string SourceId,
    string TargetId,
    ExplorerEdgeKind Kind,
    int Strength,
    string Reason,
    ExplorerEvidenceClass EvidenceClass,
    string Provenance);

public sealed record ExplorerNodePage(
    IReadOnlyList<ExplorerNode> Nodes,
    int TotalAvailable,
    bool IsTruncated,
    string? ContinuationToken);

public sealed record ExplorerNeighborhood(
    string FocusNodeId,
    IReadOnlyList<ExplorerNode> Nodes,
    IReadOnlyList<ExplorerEdge> Edges,
    bool IsTruncated,
    string? ContinuationToken);

public sealed record ExplorerConcept(
    string Name,
    string Kind,
    string Confidence,
    bool IsAiDerived,
    string Provider);

public sealed record ExplorerMediaDetails(
    string Kind,
    string? Container,
    int? Width,
    int? Height,
    double? DurationSeconds,
    string? Device,
    DateTimeOffset? CapturedAtUtc,
    string? VideoCodec,
    string? AudioCodec,
    bool HasOcrEvidence,
    bool HasTranscriptEvidence);

public sealed record ExplorerNodeDetails(
    ExplorerNode Node,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc,
    string? Summary,
    IReadOnlyList<ExplorerConcept> Topics,
    IReadOnlyList<ExplorerConcept> Entities,
    ExplorerMediaDetails? Media,
    IReadOnlyList<string> RelationshipSummaries,
    bool IsFullyIndexed);

public sealed record ExplorerChildrenRequest(
    string ParentNodeId,
    int? MaximumResults = null,
    string? ContinuationToken = null);

public sealed record ExplorerNeighborhoodRequest(
    string NodeId,
    int? Depth = null,
    int? MaximumNodes = null,
    int? MaximumEdges = null,
    bool IncludeContext = true);

public sealed record ExplorerSearchRequest(
    string Query,
    int? MaximumResults = null,
    bool IncludeContext = true);

public sealed record ExplorerSearchHit(
    ExplorerNode Node,
    int Rank,
    double Score,
    string Explanation,
    string? Snippet,
    string? EvidenceSource);

public sealed record ExplorerSearchResult(
    IReadOnlyList<ExplorerSearchHit> Results,
    bool IsTruncated,
    string Coverage,
    bool UsedAiAssistance);

public sealed record ExplorerRelatedRequest(string NodeId, int? MaximumResults = null);

public sealed record ExplorerNodeDetailsRequest(string NodeId);

public sealed record ExplorerRelatedResult(
    IReadOnlyList<ExplorerNode> Nodes,
    IReadOnlyList<ExplorerEdge> Edges,
    bool IsTruncated);

public sealed record ExplorerRequestEnvelope(
    int ProtocolMajor,
    string RequestId,
    string SessionId,
    string AuthorizationToken,
    ExplorerOperation Operation,
    JsonElement Payload);

public sealed record ExplorerProtocolError(ExplorerErrorCode Code, string Message, bool Retryable);

public sealed record ExplorerResponseEnvelope(
    int ProtocolMajor,
    string RequestId,
    bool Success,
    JsonElement? Payload,
    ExplorerProtocolError? Error);
