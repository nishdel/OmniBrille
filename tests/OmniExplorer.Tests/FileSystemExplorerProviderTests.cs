using System.Globalization;
using OmniExplorer.Core;
using OmniExplorer.Infrastructure;

namespace OmniExplorer.Tests;

public sealed class FileSystemExplorerProviderTests
{
    [Fact]
    public async Task GetDirectory_ReturnsFoldersFilesAndMetadata()
    {
        using var directory = new TemporaryDirectory();
        var childDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Folder"));
        var filePath = Path.Combine(directory.Path, "note.txt");
        await File.WriteAllTextAsync(filePath, "hello");
        var provider = new FileSystemExplorerProvider(directory.Path);

        var snapshot = await provider.GetDirectoryAsync(directory.Path, CancellationToken.None);

        Assert.Equal(ExplorerFailureKind.None, snapshot.Failure);
        Assert.Equal(2, snapshot.Children.Count);
        Assert.Contains(snapshot.Children, entry => entry.Path == childDirectory.FullName && entry.Kind == ExplorerNodeKind.Folder);
        var file = Assert.Single(snapshot.Children, entry => entry.Path == filePath);
        Assert.Equal(5, file.SizeBytes);
    }

    [Fact]
    public async Task GetDirectory_ReportsDeletedFolderWithoutThrowing()
    {
        using var directory = new TemporaryDirectory();
        var provider = new FileSystemExplorerProvider(directory.Path);
        Directory.Delete(directory.Path);

        var snapshot = await provider.GetDirectoryAsync(directory.Path, CancellationToken.None);

        Assert.Equal(ExplorerFailureKind.NotFound, snapshot.Failure);
        Assert.Empty(snapshot.Children);
    }

    [Fact]
    public async Task GetDirectory_BoundsHugeDirectoryEnumeration()
    {
        using var directory = new TemporaryDirectory();
        for (var index = 0; index < 12; index++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory.Path, $"{index:D2}.txt"),
                index.ToString(CultureInfo.InvariantCulture));
        }

        var provider = new FileSystemExplorerProvider(directory.Path, enumerationLimit: 5);

        var snapshot = await provider.GetDirectoryAsync(directory.Path, CancellationToken.None);

        Assert.Equal(5, snapshot.Children.Count);
        Assert.True(snapshot.WasTruncated);
        Assert.Equal(6, snapshot.TotalChildCount);
        Assert.Contains("protect responsiveness", snapshot.Warning);
    }

    [Fact]
    public async Task GetDirectory_RejectsAccessOutsideExplicitRoot()
    {
        using var root = new TemporaryDirectory();
        using var other = new TemporaryDirectory();
        var provider = new FileSystemExplorerProvider(root.Path);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            provider.GetDirectoryAsync(other.Path, CancellationToken.None));
    }

    [Fact]
    public async Task Search_FindsNamesAndPathsWithinSelectedRoot()
    {
        using var directory = new TemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(directory.Path, "Project-Blue"));
        await File.WriteAllTextAsync(Path.Combine(nested.FullName, "readme.md"), "content");
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "unrelated.txt"), "content");
        var provider = new FileSystemExplorerProvider(directory.Path);

        var byName = await provider.SearchAsync(new SearchRequest(directory.Path, "readme"), CancellationToken.None);
        var byPath = await provider.SearchAsync(new SearchRequest(directory.Path, "Project-Blue"), CancellationToken.None);

        Assert.Single(byName.Hits);
        Assert.Contains(byPath.Hits, hit => hit.Name == "Project-Blue");
        Assert.Contains(byPath.Hits, hit => hit.Name == "readme.md");
    }

    [Fact]
    public async Task Search_StopsAtResultBudget()
    {
        using var directory = new TemporaryDirectory();
        for (var index = 0; index < 8; index++)
        {
            await File.WriteAllTextAsync(Path.Combine(directory.Path, $"match-{index}.txt"), "x");
        }

        var provider = new FileSystemExplorerProvider(directory.Path);
        var result = await provider.SearchAsync(
            new SearchRequest(directory.Path, "match", MaxResults: 3),
            CancellationToken.None);

        Assert.Equal(3, result.Hits.Count);
        Assert.True(result.WasTruncated);
    }

    [Fact]
    public async Task Operations_HonorPreCancelledToken()
    {
        using var directory = new TemporaryDirectory();
        var provider = new FileSystemExplorerProvider(directory.Path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetDirectoryAsync(directory.Path, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.SearchAsync(new SearchRequest(directory.Path, "x"), cancellation.Token));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"OmniExplorerTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
