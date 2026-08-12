using OmniBrille.Core;

namespace OmniBrille.Infrastructure;

public sealed class FileSystemExplorerProvider : IExplorerProvider, IExplorerSearchProvider
{
    public const int DefaultEnumerationLimit = 5_000;

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

    public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(path);
        EnsureWithinAccessRoot(normalized);
        return Task.Run(() => EnumerateDirectory(normalized, cancellationToken), cancellationToken);
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

    private ExplorerDirectorySnapshot EnumerateDirectory(string path, CancellationToken cancellationToken)
    {
        var focus = CreateFocus(path);
        if (!Directory.Exists(path))
        {
            return new ExplorerDirectorySnapshot(
                focus,
                [],
                ExplorerFailureKind.NotFound,
                "This folder no longer exists or is unavailable.");
        }

        var children = new List<ExplorerEntry>(Math.Min(_enumerationLimit, 256));
        var skippedEntries = 0;
        var observedCount = 0;
        var wasTruncated = false;

        try
        {
            foreach (var childPath in Directory.EnumerateFileSystemEntries(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                observedCount++;

                if (children.Count >= _enumerationLimit)
                {
                    wasTruncated = true;
                    break;
                }

                try
                {
                    children.Add(CreateEntry(childPath));
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    skippedEntries++;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return new ExplorerDirectorySnapshot(
                focus,
                children,
                ExplorerFailureKind.AccessDenied,
                "Access to this folder was denied.",
                observedCount,
                wasTruncated);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return new ExplorerDirectorySnapshot(
                focus,
                children,
                ExplorerFailureKind.EnumerationFailed,
                $"The folder could not be fully read: {exception.Message}",
                observedCount,
                wasTruncated);
        }

        var warningParts = new List<string>();
        if (wasTruncated)
        {
            warningParts.Add($"Enumeration stopped after {_enumerationLimit:N0} items to protect responsiveness");
        }

        if (skippedEntries > 0)
        {
            warningParts.Add($"{skippedEntries:N0} unreadable entries were skipped");
        }

        return new ExplorerDirectorySnapshot(
            focus,
            children,
            ExplorerFailureKind.None,
            warningParts.Count == 0 ? null : string.Join(". ", warningParts) + ".",
            wasTruncated ? observedCount : children.Count,
            wasTruncated);
    }

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
}
