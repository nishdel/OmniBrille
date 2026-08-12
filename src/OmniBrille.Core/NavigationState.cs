namespace OmniBrille.Core;

public sealed class NavigationState
{
    private readonly List<string> _history = [];

    public string? AccessRoot { get; private set; }

    public string? CurrentPath { get; private set; }

    public ExplorerProviderMode Mode { get; private set; } = ExplorerProviderMode.Standalone;

    public bool CanGoBack => _history.Count > 0;

    public IReadOnlyList<string> History => _history;

    public void Clear()
    {
        AccessRoot = null;
        CurrentPath = null;
        Mode = ExplorerProviderMode.Standalone;
        _history.Clear();
    }

    public void SetRoot(string path, ExplorerProviderMode mode = ExplorerProviderMode.Standalone)
    {
        Mode = mode;
        var normalized = Normalize(path, mode);
        AccessRoot = normalized;
        CurrentPath = normalized;
        _history.Clear();
    }

    public void NavigateTo(string path)
    {
        EnsureInitialized();
        var normalized = Normalize(path, Mode);
        if (Mode == ExplorerProviderMode.Standalone && !PathBoundary.IsWithin(AccessRoot!, normalized))
        {
            throw new InvalidOperationException("Navigation cannot leave the explicitly selected access root.");
        }

        if (PathBoundary.Comparer.Equals(CurrentPath, normalized))
        {
            return;
        }

        _history.Add(CurrentPath!);
        CurrentPath = normalized;
    }

    public string? GoBack()
    {
        EnsureInitialized();
        if (_history.Count == 0)
        {
            return null;
        }

        var index = _history.Count - 1;
        CurrentPath = _history[index];
        _history.RemoveAt(index);
        return CurrentPath;
    }

    private static string Normalize(string path, ExplorerProviderMode mode)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl))
        {
            throw new ArgumentException("A navigation target is required.", nameof(path));
        }

        return mode == ExplorerProviderMode.Standalone
            ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))
            : path;
    }

    private void EnsureInitialized()
    {
        if (AccessRoot is null || CurrentPath is null)
        {
            throw new InvalidOperationException("Choose an access root before navigating.");
        }
    }
}

public static class PathBoundary
{
    public static StringComparer Comparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static StringComparison Comparison { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static bool IsWithin(string rootPath, string candidatePath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));

        if (Comparer.Equals(root, candidate))
        {
            return true;
        }

        return candidate.StartsWith(root + Path.DirectorySeparatorChar, Comparison);
    }
}
