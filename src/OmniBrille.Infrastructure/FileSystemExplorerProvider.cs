using System.Runtime.CompilerServices;
using OmniBrille.Core;

namespace OmniBrille.Infrastructure;

public sealed class FileSystemExplorerProvider :
    IExplorerProvider,
    IProgressiveExplorerProvider,
    IExplorerSearchProvider
{
    public const int DefaultEnumerationLimit = 5_000;
    public const int DefaultBatchSize = 32;

    private readonly int _enumerationLimit;

    public FileSystemExplorerProvider(string accessRoot, int enumerationLimit = DefaultEnumerationLimit)
    {
        if (string.IsNullOrWhiteSpace(accessRoot))
        {
            throw new ArgumentException("An explicitly selected access root is required.", nameof(accessRoot));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(enumerationLimit, 1);

        AccessRoot = Normalize(accessRoot);
        _enumerationLimit = enumerationLimit;
    }

    public string AccessRoot { get; }

    public async Task<ExplorerDirectorySnapshot> GetDirectoryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var children = new List<ExplorerEntry>();
        ExplorerDirectoryBatch? final = null;
        await foreach (var batch in GetDirectoryBatchesAsync(path, 128, cancellationToken))
        {
            children.AddRange(batch.AddedChildren);
            final = batch;
        }

        if (final is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Directory enumeration produced no result.");
        }

        return new ExplorerDirectorySnapshot(
            final.Focus,
            children,
            final.Failure,
            final.Warning,
            final.TotalChildCount ?? final.ItemsObserved,
            final.WasTruncated);
    }

    public IAsyncEnumerable<ExplorerDirectoryBatch> GetDirectoryBatchesAsync(
        string path,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var normalized = Normalize(path);
        EnsureWithinAccessRoot(normalized);
        return EnumerateBatchesAsync(normalized, batchSize, cancellationToken);
    }

    public Task<ExplorerSearchResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var root = Normalize(request.RootPath);
        EnsureWithinAccessRoot(root);

        if (request.MaxResults < 1 || request.MaxDirectories < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Search bounds must be positive.");
        }

        return Task.Run(() => SearchCore(request with { RootPath = root }, cancellationToken), cancellationToken);
    }

    private async IAsyncEnumerable<ExplorerDirectoryBatch> EnumerateBatchesAsync(
        string path,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var focus = await Task.Run(() => CreateFocus(path), cancellationToken);
        if (!Directory.Exists(path))
        {
            yield return new ExplorerDirectoryBatch(
                focus,
                [],
                0,
                true,
                ExplorerFailureKind.NotFound,
                "This folder no longer exists or is unavailable.",
                0);
            yield break;
        }

        EnumerationState? state = null;
        ExplorerDirectoryBatch? initializationFailure = null;
        try
        {
            state = await Task.Run(
                () => new EnumerationState(Directory.EnumerateFileSystemEntries(path).GetEnumerator()),
                cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            initializationFailure = FailureBatch(
                focus,
                ExplorerFailureKind.AccessDenied,
                "Access to this folder was denied.");
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            initializationFailure = FailureBatch(
                focus,
                ExplorerFailureKind.EnumerationFailed,
                $"The folder could not be read: {exception.Message}");
        }

        if (initializationFailure is not null)
        {
            yield return initializationFailure;
            yield break;
        }

        var activeState = state!;
        using (activeState)
        {
            yield return new ExplorerDirectoryBatch(focus, [], 0, false);

            while (!activeState.IsComplete)
            {
                var batch = await Task.Run(
                    () => ReadBatch(focus, activeState, batchSize, cancellationToken),
                    cancellationToken);
                yield return batch;
            }
        }
    }

    private ExplorerDirectoryBatch ReadBatch(
        ExplorerEntry focus,
        EnumerationState state,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var added = new List<ExplorerEntry>(batchSize);
        while (added.Count < batchSize && !state.IsComplete)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool hasNext;
            try
            {
                hasNext = state.Enumerator.MoveNext();
            }
            catch (UnauthorizedAccessException)
            {
                state.Failure = ExplorerFailureKind.AccessDenied;
                state.FailureMessage = "Access to this folder was denied during enumeration.";
                state.IsComplete = true;
                break;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                state.Failure = ExplorerFailureKind.EnumerationFailed;
                state.FailureMessage = $"The folder could not be fully read: {exception.Message}";
                state.IsComplete = true;
                break;
            }

            if (!hasNext)
            {
                state.IsComplete = true;
                break;
            }

            state.ItemsObserved++;
            if (state.ValidItemCount >= _enumerationLimit)
            {
                state.WasTruncated = true;
                state.IsComplete = true;
                break;
            }

            try
            {
                added.Add(CreateEntry(state.Enumerator.Current));
                state.ValidItemCount++;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                state.SkippedEntries++;
            }
        }

        var warning = state.IsComplete ? BuildEnumerationWarning(state) : null;
        return new ExplorerDirectoryBatch(
            focus,
            added,
            state.ItemsObserved,
            state.IsComplete,
            state.Failure,
            warning,
            state.ItemsObserved,
            state.WasTruncated);
    }

    private string? BuildEnumerationWarning(EnumerationState state)
    {
        var warningParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(state.FailureMessage))
        {
            warningParts.Add(state.FailureMessage);
        }

        if (state.WasTruncated)
        {
            warningParts.Add($"Enumeration stopped after {_enumerationLimit:N0} items to protect responsiveness");
        }

        if (state.SkippedEntries > 0)
        {
            warningParts.Add($"{state.SkippedEntries:N0} unreadable entries were skipped");
        }

        return warningParts.Count == 0 ? null : string.Join(". ", warningParts) + ".";
    }

    private static ExplorerDirectoryBatch FailureBatch(
        ExplorerEntry focus,
        ExplorerFailureKind failure,
        string warning) => new(focus, [], 0, true, failure, warning, 0);

    private static ExplorerSearchResult SearchCore(SearchRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query.Trim();
        if (query.Length == 0)
        {
            return new ExplorerSearchResult([], false, 0);
        }

        var hits = new List<ExplorerSearchHit>(Math.Min(request.MaxResults, 80));
        var pending = new Queue<string>();
        pending.Enqueue(request.RootPath);
        var directoriesVisited = 0;
        var inaccessibleDirectories = 0;
        var truncated = false;

        while (pending.Count > 0 && directoriesVisited < request.MaxDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Dequeue();
            directoriesVisited++;

            try
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ExplorerEntry entry;
                    try
                    {
                        entry = CreateEntry(path);
                    }
                    catch (Exception exception) when (IsRecoverable(exception))
                    {
                        continue;
                    }

                    if (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        entry.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        hits.Add(new ExplorerSearchHit(entry.Id, entry.Name, entry.Path, entry.Kind));
                        if (hits.Count >= request.MaxResults)
                        {
                            truncated = true;
                            return CompleteSearch(hits, truncated, directoriesVisited, inaccessibleDirectories);
                        }
                    }

                    if (entry.Kind == ExplorerNodeKind.Folder && entry.IsNavigable)
                    {
                        pending.Enqueue(entry.Path);
                    }
                }
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                inaccessibleDirectories++;
            }
        }

        if (pending.Count > 0)
        {
            truncated = true;
        }

        return CompleteSearch(hits, truncated, directoriesVisited, inaccessibleDirectories);
    }

    private static ExplorerSearchResult CompleteSearch(
        List<ExplorerSearchHit> hits,
        bool truncated,
        int directoriesVisited,
        int inaccessibleDirectories)
    {
        hits.Sort((left, right) =>
        {
            var nameComparison = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return nameComparison != 0
                ? nameComparison
                : StringComparer.OrdinalIgnoreCase.Compare(left.Path, right.Path);
        });

        var warning = inaccessibleDirectories > 0
            ? $"{inaccessibleDirectories:N0} inaccessible folders were skipped."
            : null;
        return new ExplorerSearchResult(hits, truncated, directoriesVisited, warning);
    }

    private static ExplorerEntry CreateFocus(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = path;
        }

        DateTimeOffset? modified = null;
        try
        {
            modified = Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path) : null;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
        }

        return new ExplorerEntry(path, name, path, ExplorerNodeKind.Folder, null, modified);
    }

    private static ExplorerEntry CreateEntry(string path)
    {
        var attributes = File.GetAttributes(path);
        var isDirectory = attributes.HasFlag(FileAttributes.Directory);
        var isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);
        long? size = null;
        DateTimeOffset? modified;

        if (isDirectory)
        {
            modified = Directory.GetLastWriteTimeUtc(path);
        }
        else
        {
            var file = new FileInfo(path);
            size = file.Length;
            modified = file.LastWriteTimeUtc;
        }

        var normalized = Normalize(path);
        return new ExplorerEntry(
            normalized,
            Path.GetFileName(normalized),
            normalized,
            isDirectory ? ExplorerNodeKind.Folder : ExplorerNodeKind.File,
            size,
            modified,
            isReparsePoint,
            isDirectory && !isReparsePoint);
    }

    private void EnsureWithinAccessRoot(string path)
    {
        if (!PathBoundary.IsWithin(AccessRoot, path))
        {
            throw new UnauthorizedAccessException("The requested path is outside the explicitly selected access root.");
        }
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsRecoverable(Exception exception) => exception is
        UnauthorizedAccessException or
        IOException or
        System.Security.SecurityException or
        ArgumentException or
        NotSupportedException;

    private sealed class EnumerationState(IEnumerator<string> enumerator) : IDisposable
    {
        public IEnumerator<string> Enumerator { get; } = enumerator;

        public int ItemsObserved { get; set; }

        public int ValidItemCount { get; set; }

        public int SkippedEntries { get; set; }

        public bool IsComplete { get; set; }

        public bool WasTruncated { get; set; }

        public ExplorerFailureKind Failure { get; set; }

        public string? FailureMessage { get; set; }

        public void Dispose() => Enumerator.Dispose();
    }
}
