namespace OmniBrille.Core;

public sealed class NavigationState
{
    private readonly List<string> _history = [];

    public string? AccessRoot { get; private set; }

    public string? CurrentPath { get; private set; }

    public bool CanGoBack => _history.Count > 0;

    public IReadOnlyList<string> History => _history;

    public void SetRoot(string path)
    {
        var normalized = Normalize(path);
        AccessRoot = normalized;
        CurrentPath = normalized;
        _history.Clear();
    }

    public void NavigateTo(string path)
    {
        EnsureInitialized();
        var normalized = Normalize(path);
        if (!PathBoundary.IsWithin(AccessRoot!, normalized))
        {
            throw new InvalidOperationException("Navigation cannot leave the explicitly selected access root.");
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(CurrentPath, normalized))
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

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

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
    public static bool IsWithin(string rootPath, string candidatePath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));

        if (StringComparer.OrdinalIgnoreCase.Equals(root, candidate))
        {
            return true;
        }

        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
