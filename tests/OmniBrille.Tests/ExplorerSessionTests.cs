using OmniBrille.Core;
using OmniBrille.Desktop.Presentation;

namespace OmniBrille.Tests;

public sealed class ExplorerSessionTests
{
    [Fact]
    public async Task ProviderGeneration_ChangesWhenAuthorityIsOpenedOrReset()
    {
        var root = Normalize("voice-generation-root");
        var provider = new FakeProvider(root, [Snapshot(root)]);
        using var session = new ExplorerSession();
        var initial = session.ProviderGeneration;

        await session.OpenRootAsync(provider, provider);
        var opened = session.ProviderGeneration;
        session.Reset();

        Assert.True(opened > initial);
        Assert.True(session.ProviderGeneration > opened);
    }

    [Fact]
    public async Task NavigateAndBack_ChangeFocusAndPreserveContext()
    {
        var root = Normalize("root");
        var child = Path.Combine(root, "child");
        var provider = new FakeProvider(root,
        [
            Snapshot(root, Entry(child, ExplorerNodeKind.Folder)),
            Snapshot(child, Entry(Path.Combine(child, "file.txt"), ExplorerNodeKind.File)),
        ]);
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        Assert.True(await session.NavigateAsync(child));
        Assert.Equal(child, session.CurrentPath);
        Assert.True(session.CanGoBack);
        Assert.Contains(session.Neighborhood!.Nodes, node => node.Kind == ExplorerNodeKind.Context && node.Path == root);

        Assert.True(await session.GoBackAsync());
        Assert.Equal(root, session.CurrentPath);
    }

    [Fact]
    public async Task TrailJump_PrunesCrossedHistoryAndBackContinuesChronologically()
    {
        var root = Normalize("trail-root");
        var first = Path.Combine(root, "first");
        var second = Path.Combine(first, "second");
        var third = Path.Combine(second, "third");
        var provider = new FakeProvider(root, [Snapshot(root), Snapshot(first), Snapshot(second), Snapshot(third)]);
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        Assert.True(await session.NavigateAsync(first));
        Assert.True(await session.NavigateAsync(second));
        Assert.True(await session.NavigateAsync(third));

        Assert.Equal(["second", "first", Path.GetFileName(root)], session.NavigationTrail.Select(entry => entry.DisplayName));
        var destination = session.NavigationTrail[1];
        Assert.True(await session.NavigateTrailAsync(destination));
        Assert.Equal(first, session.CurrentPath);
        Assert.Equal(1, session.NavigationHistoryCount);
        Assert.False(await session.NavigateTrailAsync(destination));
        Assert.True(await session.GoBackAsync());
        Assert.Equal(root, session.CurrentPath);
        Assert.Empty(session.NavigationTrail);
    }

    [Fact]
    public async Task TrailProjection_IsBoundedAndRejectsEntriesAfterNavigationOrProviderReplacement()
    {
        var root = Normalize("bounded-trail");
        var folders = Enumerable.Range(0, 9).Select(index => Path.Combine(root, $"folder-{index}")).ToArray();
        var provider = new FakeProvider(root, folders.Prepend(root).Select(path => Snapshot(path)));
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        foreach (var folder in folders)
        {
            Assert.True(await session.NavigateAsync(folder));
        }

        Assert.Equal(6, session.NavigationTrail.Count);
        var oldEntry = session.NavigationTrail[0];
        Assert.True(await session.NavigateAsync(root));
        Assert.False(await session.NavigateTrailAsync(oldEntry));
        var providerEntry = session.NavigationTrail[0];
        await session.OpenRootAsync(provider, provider);
        Assert.False(await session.NavigateTrailAsync(providerEntry));
        Assert.Empty(session.NavigationTrail);
    }

    [Fact]
    public async Task FailedTrailJump_RetainsFocusHistoryAndAggregatePage()
    {
        var root = Normalize("failed-trail");
        var child = Path.Combine(root, "child");
        var files = Enumerable.Range(0, 30)
            .Select(index => Entry(Path.Combine(child, $"file-{index}.txt"), ExplorerNodeKind.File)).ToArray();
        var provider = new FakeProvider(root, [Snapshot(root), Snapshot(child, files)]);
        using var session = new ExplorerSession(new GraphNeighborhoodBuilder(8));
        await session.OpenRootAsync(provider, provider);
        Assert.True(await session.NavigateAsync(child));
        var aggregate = Assert.Single(session.Neighborhood!.Nodes, node => node.Kind == ExplorerNodeKind.Aggregate);
        Assert.True(session.ActivateAggregate(aggregate.Id));
        var destination = session.NavigationTrail[1];
        var priorScene = session.Neighborhood;
        provider.ReplaceSnapshot(new ExplorerDirectorySnapshot(
            Entry(root, ExplorerNodeKind.Folder), [], ExplorerFailureKind.NotFound, "gone"));

        Assert.False(await session.NavigateTrailAsync(destination));

        Assert.Same(priorScene, session.Neighborhood);
        Assert.True(session.IsAggregateRefined);
        Assert.Equal(1, session.NavigationHistoryCount);
        Assert.True(await session.NavigateTrailAsync(session.NavigationTrail[0]));
        Assert.False(session.IsAggregateRefined);
        Assert.Equal(child, session.CurrentPath);
    }

    [Fact]
    public async Task TrailJump_LateFailureCannotRestoreSceneAfterProviderReplacement()
    {
        var root = Normalize("late-trail");
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        var provider = new ControllableProvider(root, Snapshot(root));
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        var openingFirst = session.NavigateAsync(first);
        provider.Complete(first, Snapshot(first));
        Assert.True(await openingFirst);
        var openingSecond = session.NavigateAsync(second);
        provider.Complete(second, Snapshot(second));
        Assert.True(await openingSecond);
        provider.Defer(first);
        var entry = session.NavigationTrail[0];
        var jumping = session.NavigateTrailAsync(entry);
        Assert.Empty(session.NavigationTrail);
        Assert.False(await session.NavigateTrailAsync(entry));
        var replacementRoot = Normalize("replacement-trail");
        var replacement = new FakeProvider(replacementRoot, [Snapshot(replacementRoot)]);
        await session.OpenRootAsync(replacement, replacement);
        provider.Fail(first);

        Assert.False(await jumping);
        Assert.Equal(replacementRoot, session.CurrentPath);
        Assert.Empty(session.NavigationTrail);
        Assert.False(session.CanGoBack);
    }

    [Fact]
    public async Task CancelledTrailJump_DoesNotCommitAProviderThatIgnoresCancellation()
    {
        var root = Normalize("cancelled-trail");
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        var provider = new ControllableProvider(root, Snapshot(root));
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        var openingFirst = session.NavigateAsync(first);
        provider.Complete(first, Snapshot(first));
        Assert.True(await openingFirst);
        var openingSecond = session.NavigateAsync(second);
        provider.Complete(second, Snapshot(second));
        Assert.True(await openingSecond);
        provider.Defer(first);
        using var cancellation = new CancellationTokenSource();
        var jumping = session.NavigateTrailAsync(session.NavigationTrail[0], cancellation.Token);
        cancellation.Cancel();
        provider.Complete(first, Snapshot(first));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => jumping);
        Assert.Equal(second, session.CurrentPath);
        Assert.Equal(2, session.NavigationHistoryCount);
    }

    [Fact]
    public async Task DirectoryPreviews_AreBoundedActualChildrenOfAdmittedParents()
    {
        var root = PreviewEntry("opaque-root", "Authorized root");
        var parents = Enumerable.Range(0, 6).Select(index =>
            PreviewEntry($"opaque-parent-{index}", $"Folder {index}", root.Target)).ToArray();
        var provider = new PreviewProvider(new ExplorerDirectorySnapshot(root, parents));
        foreach (var parent in parents)
        {
            provider.Previews[parent.Target] = new ExplorerDirectorySnapshot(parent,
                Enumerable.Range(0, 5).Select(index =>
                    PreviewEntry($"{parent.Id}-child-{index}", $"Subfolder {index}", parent.Target)).ToArray());
        }
        using var session = new ExplorerSession();
        var baseSceneWasReady = false;
        session.StateChanged += (_, _) => baseSceneWasReady |=
            session.LoadState == ExplorerLoadState.Ready && session.Neighborhood?.Nodes.Count == 7;

        await session.OpenRootAsync(provider, provider);

        Assert.True(baseSceneWasReady);
        Assert.Equal(4, provider.PreviewRequests.Count);
        Assert.All(provider.PreviewRequests, target => Assert.Contains(parents, parent => parent.Target == target));
        var previews = session.Neighborhood!.Nodes.Where(node =>
            ExplorerSceneSemantics.RelationOf(session.Neighborhood, node) == ExplorerSceneRelation.DescendantPreview).ToArray();
        Assert.Equal(12, previews.Length);
        Assert.Equal(6, session.Neighborhood.TotalChildCount);
        Assert.Equal(0, session.Neighborhood.HiddenChildCount);
        Assert.All(previews, preview =>
        {
            Assert.Contains(session.Neighborhood.Edges, edge =>
                edge.TargetId == preview.Id && edge.SourceId == preview.ParentNavigationTarget);
            Assert.DoesNotContain(session.Neighborhood.Edges, edge => edge.SourceId == root.Id && edge.TargetId == preview.Id);
        });
        Assert.InRange(session.Neighborhood.Nodes.Count, 1, session.SceneBudget);
    }

    [Fact]
    public async Task DirectoryPreviews_UseOnlyFreeSceneSlotsAndSkipFullScenes()
    {
        var root = PreviewEntry("root", "Root");
        var parents = Enumerable.Range(0, 4).Select(index => PreviewEntry($"parent-{index}", $"Folder {index}", root.Id)).ToArray();
        var provider = new PreviewProvider(new ExplorerDirectorySnapshot(root, parents));
        foreach (var parent in parents)
        {
            provider.Previews[parent.Target] = new ExplorerDirectorySnapshot(parent,
                Enumerable.Range(0, 3).Select(index => PreviewEntry($"{parent.Id}-child-{index}", "Child", parent.Id)).ToArray());
        }
        using var session = new ExplorerSession(new GraphNeighborhoodBuilder(6));
        await session.OpenRootAsync(provider, provider);

        Assert.Equal(6, session.Neighborhood!.Nodes.Count);
        Assert.Single(provider.PreviewRequests);
        Assert.All(parents, parent => Assert.Contains(session.Neighborhood.Nodes, node => node.Id == parent.Id));

        var fullProvider = new PreviewProvider(new ExplorerDirectorySnapshot(root, parents));
        using var fullSession = new ExplorerSession(new GraphNeighborhoodBuilder(5));
        await fullSession.OpenRootAsync(fullProvider, fullProvider);
        Assert.Empty(fullProvider.PreviewRequests);
        Assert.Equal(5, fullSession.Neighborhood!.Nodes.Count);
    }

    [Fact]
    public async Task DirectoryPreviews_RejectWrongFocusParentAndFailuresWithoutReplacingPrimaryScene()
    {
        var root = PreviewEntry("root", "Root");
        var parents = Enumerable.Range(0, 4).Select(index => PreviewEntry($"parent-{index}", $"Folder {index}", root.Id)).ToArray();
        var provider = new PreviewProvider(new ExplorerDirectorySnapshot(root, parents));
        provider.Previews[parents[0].Target] = new ExplorerDirectorySnapshot(
            PreviewEntry("unrelated", "Wrong focus"), [PreviewEntry("wrong-focus-child", "Wrong", parents[0].Target)]);
        provider.Previews[parents[1].Target] = new ExplorerDirectorySnapshot(parents[1],
            [PreviewEntry("wrong-parent-child", "Wrong", parents[0].Target)]);
        provider.FailingTarget = parents[2].Target;
        provider.Previews[parents[3].Target] = new ExplorerDirectorySnapshot(parents[3],
            [PreviewEntry("valid-child", "Valid", parents[3].Target)]);
        using var session = new ExplorerSession();

        await session.OpenRootAsync(provider, provider);

        Assert.Equal(ExplorerLoadState.Ready, session.LoadState);
        Assert.Equal(root.Id, session.Neighborhood!.FocusNodeId);
        Assert.Contains(session.Neighborhood.Nodes, node => node.Id == "valid-child");
        Assert.DoesNotContain(session.Neighborhood.Nodes, node => node.Id is "wrong-focus-child" or "wrong-parent-child");
        Assert.Equal(4, provider.PreviewRequests.Count);
        Assert.False(session.CanGoBack);
    }

    [Fact]
    public async Task DirectoryPreviews_LateResponseCannotReplaceNewNavigation()
    {
        var root = PreviewEntry("root", "Root");
        var first = PreviewEntry("first", "First", root.Id);
        var second = PreviewEntry("second", "Second", root.Id);
        var provider = new PreviewProvider(new ExplorerDirectorySnapshot(root, [first, second])) { DeferredTarget = first.Target };
        provider.Directories[second.Target] = new ExplorerDirectorySnapshot(second, []);
        using var session = new ExplorerSession();
        var opening = session.OpenRootAsync(provider, provider);
        Assert.Equal(ExplorerLoadState.Ready, session.LoadState);
        Assert.Single(provider.PreviewRequests);

        Assert.True(await session.NavigateAsync(second.Target));
        provider.DeferredPreview.SetResult(new ExplorerDirectorySnapshot(first,
            [PreviewEntry("late-child", "Late", first.Id)]));
        await opening;

        Assert.Equal(second.Id, session.Neighborhood!.FocusNodeId);
        Assert.DoesNotContain(session.Neighborhood.Nodes, node => node.Id == "late-child");
        Assert.Equal(1, session.NavigationHistoryCount);
        Assert.Single(provider.PreviewRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectoryPreviews_LateCompletionCannotReplaceNewProvider(bool fail)
    {
        var root = PreviewEntry("root", "Root");
        var folder = PreviewEntry("folder", "Folder", root.Id);
        var provider = new PreviewProvider(new ExplorerDirectorySnapshot(root, [folder])) { DeferredTarget = folder.Target };
        using var session = new ExplorerSession();
        var opening = session.OpenRootAsync(provider, provider);
        var replacementRoot = PreviewEntry("replacement", "Replacement");
        var replacement = new PreviewProvider(new ExplorerDirectorySnapshot(replacementRoot, []));
        await session.OpenRootAsync(replacement, replacement);
        if (fail)
        {
            provider.DeferredPreview.SetException(new IOException("Obsolete preview failure."));
        }
        else
        {
            provider.DeferredPreview.SetResult(new ExplorerDirectorySnapshot(folder,
                [PreviewEntry("obsolete-child", "Obsolete", folder.Id)]));
        }
        await opening;

        Assert.Equal(replacementRoot.Id, session.Neighborhood!.FocusNodeId);
        Assert.Equal(ExplorerLoadState.Ready, session.LoadState);
        Assert.Empty(session.NavigationTrail);
    }

    [Fact]
    public async Task DirectoryPreviews_LateResponseCannotReplaceContextModeOrFilters()
    {
        var root = PreviewEntry("root", "Root");
        var folder = PreviewEntry("folder", "Folder", root.Id);
        var provider = new PreviewProvider(new ExplorerDirectorySnapshot(root, [folder])) { DeferredTarget = folder.Target };
        using var session = new ExplorerSession();
        var opening = session.OpenRootAsync(provider, provider);
        Assert.True(await session.SwitchToContextAsync(folder.Target));
        Assert.True(session.ApplyContextFilter(new ContextFilter(ExplorerRelationshipKind.Topic)));
        var filteredScene = session.Neighborhood;
        provider.DeferredPreview.SetResult(new ExplorerDirectorySnapshot(folder,
            [PreviewEntry("late-child", "Late", folder.Id)]));
        await opening;

        Assert.Same(filteredScene, session.Neighborhood);
        Assert.Equal(ExplorerViewMode.Context, session.ViewMode);
        Assert.True(session.ContextFilter.IsActive);
        Assert.DoesNotContain(session.Neighborhood!.Nodes, node => node.Id == "late-child");
    }

    [Fact]
    public async Task DirectoryPreviews_CancellationKeepsTheAlreadyReadyPrimaryScene()
    {
        var root = PreviewEntry("root", "Root");
        var folder = PreviewEntry("folder", "Folder", root.Id);
        var provider = new PreviewProvider(new ExplorerDirectorySnapshot(root, [folder])) { DeferredTarget = folder.Target };
        using var session = new ExplorerSession();
        using var cancellation = new CancellationTokenSource();
        var opening = session.OpenRootAsync(provider, provider, cancellation.Token);
        cancellation.Cancel();
        provider.DeferredPreview.SetResult(new ExplorerDirectorySnapshot(folder,
            [PreviewEntry("cancelled-child", "Cancelled", folder.Id)]));
        await opening;

        Assert.Equal(ExplorerLoadState.Ready, session.LoadState);
        Assert.Equal(root.Id, session.Neighborhood!.FocusNodeId);
        Assert.DoesNotContain(session.Neighborhood.Nodes, node => node.Id == "cancelled-child");
    }

    [Fact]
    public async Task UpAndRoot_UseProviderAuthoredContainmentWithoutChangingBackSemantics()
    {
        var root = Normalize("up-root");
        var child = Path.Combine(root, "child");
        var grandchild = Path.Combine(child, "grandchild");
        var rootEntry = EntryWithParent(root, null);
        var childEntry = EntryWithParent(child, root);
        var grandchildEntry = EntryWithParent(grandchild, child);
        var provider = new FakeProvider(root,
        [
            new ExplorerDirectorySnapshot(rootEntry, [childEntry]),
            new ExplorerDirectorySnapshot(childEntry, [grandchildEntry]),
            new ExplorerDirectorySnapshot(grandchildEntry, []),
        ]);
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        Assert.True(await session.NavigateAsync(child));
        Assert.True(await session.NavigateAsync(grandchild));

        Assert.True(session.CanGoUp);
        Assert.True(await session.GoUpAsync());
        Assert.Equal(child, session.CurrentPath);
        Assert.True(await session.GoRootAsync());
        Assert.Equal(root, session.CurrentPath);
        Assert.True(session.CanGoBack);
    }

    [Fact]
    public async Task FailedNavigation_DoesNotReplaceCurrentFocus()
    {
        var root = Normalize("root");
        var missing = Path.Combine(root, "missing");
        var provider = new FakeProvider(root,
        [
            Snapshot(root, Entry(missing, ExplorerNodeKind.Folder)),
            new ExplorerDirectorySnapshot(
                Entry(missing, ExplorerNodeKind.Folder),
                [],
                ExplorerFailureKind.NotFound,
                "gone"),
        ]);
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        Assert.False(await session.NavigateAsync(missing));
        Assert.Equal(root, session.CurrentPath);
        Assert.Equal("gone", session.Status);
    }

    [Fact]
    public async Task Search_HighlightsVisibleMatches()
    {
        var root = Normalize("root");
        var match = Entry(Path.Combine(root, "match.txt"), ExplorerNodeKind.File);
        var provider = new FakeProvider(root, [Snapshot(root, match)])
        {
            SearchResult = new ExplorerSearchResult(
                [new ExplorerSearchHit(match.Id, match.Name, match.Path, match.Kind)],
                false,
                1),
        };
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        await session.SearchAsync("match");

        Assert.Contains(match.Id, session.HighlightedNodeIds);
        Assert.Single(session.SearchResult!.Hits);
    }

    [Fact]
    public void SearchHit_AccessibleTextOmitsOpaqueSessionIdentifiers()
    {
        var hit = new ExplorerSearchHit(
            "opaque-result-id",
            "report.txt",
            "Authorized root / report.txt",
            ExplorerNodeKind.File,
            "opaque-navigation-id",
            "opaque-parent-id");

        var accessibleText = hit.ToString();

        Assert.Equal("report.txt, File, Authorized root / report.txt", accessibleText);
        Assert.DoesNotContain("opaque", accessibleText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileSearchFocus_PinsMatchIntoBoundedGraph()
    {
        var root = Normalize("root");
        var children = Enumerable.Range(0, 20)
            .Select(index => Entry(Path.Combine(root, $"file-{index:D2}.txt"), ExplorerNodeKind.File))
            .ToArray();
        var match = children[^1];
        var provider = new FakeProvider(root, [Snapshot(root, children)]);
        using var session = new ExplorerSession(new GraphNeighborhoodBuilder(6));
        await session.OpenRootAsync(provider, provider);

        var focused = await session.FocusSearchHitAsync(
            new ExplorerSearchHit(match.Id, match.Name, match.Path, match.Kind));

        Assert.True(focused);
        Assert.Equal(match.Id, session.SelectedNode!.Id);
        Assert.Contains(session.Neighborhood!.Nodes, node => node.Id == match.Id);
        Assert.Equal(6, session.Neighborhood.Nodes.Count);
    }

    [Fact]
    public async Task AggregateRefinement_ActivatesAndBackRestoresOverview()
    {
        var root = Normalize("aggregate-root");
        var children = Enumerable.Range(0, 30)
            .Select(index => Entry(Path.Combine(root, $"file-{index:D2}.txt"), ExplorerNodeKind.File))
            .ToArray();
        var provider = new FakeProvider(root, [Snapshot(root, children)]);
        using var session = new ExplorerSession(new GraphNeighborhoodBuilder(8));
        await session.OpenRootAsync(provider, provider);
        var overviewIds = session.Neighborhood!.Nodes.Select(node => node.Id).ToArray();
        var aggregate = Assert.Single(session.Neighborhood.Nodes, node => node.Kind == ExplorerNodeKind.Aggregate);

        Assert.True(session.ActivateAggregate(aggregate.Id));
        Assert.True(session.IsAggregateRefined);
        Assert.True(session.CanGoBack);
        Assert.NotEqual(overviewIds, session.Neighborhood.Nodes.Select(node => node.Id));

        Assert.True(await session.GoBackAsync());
        Assert.False(session.IsAggregateRefined);
        Assert.Equal(overviewIds, session.Neighborhood.Nodes.Select(node => node.Id));
        Assert.False(session.CanGoBack);
    }

    [Fact]
    public async Task ProgressiveLoad_ExposesInteractiveShellAndPartialState()
    {
        var root = Normalize("progressive-root");
        var provider = new ProgressiveFakeProvider(root);
        using var session = new ExplorerSession();

        var opening = session.OpenRootAsync(provider, provider);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ExplorerLoadState.Loading, session.LoadState);
        Assert.NotNull(session.Neighborhood);
        Assert.Equal(root, session.Neighborhood.Focus.Path);

        var partialStateObserved = ObserveSessionStateAsync(
            session,
            () => session.LoadState == ExplorerLoadState.PartiallyLoaded);
        provider.Publish([Entry(Path.Combine(root, "one.txt"), ExplorerNodeKind.File)], isComplete: false);
        await partialStateObserved;
        Assert.Equal(1, session.LoadedItemCount);
        Assert.NotNull(session.Neighborhood);

        provider.Publish([Entry(Path.Combine(root, "two.txt"), ExplorerNodeKind.File)], isComplete: true);
        await opening;
        Assert.Equal(ExplorerLoadState.Ready, session.LoadState);
        Assert.Equal(2, session.LoadedItemCount);
    }

    [Fact]
    public async Task DenseProgressiveLoad_CoalescesProjectionAndKeepsFinalSceneBounded()
    {
        var root = Normalize("progressive-dense-root");
        var provider = new BurstProgressiveProvider(root, 5_000);
        using var session = new ExplorerSession();
        var stateChanges = 0;
        session.StateChanged += (_, _) => stateChanges++;

        await session.OpenRootAsync(provider, provider);

        Assert.Equal(5_000, session.LoadedItemCount);
        Assert.Equal(ExplorerLoadState.Ready, session.LoadState);
        Assert.InRange(session.Neighborhood!.Nodes.Count, 1, GraphNeighborhoodBuilder.DefaultNodeBudget);
        Assert.InRange(stateChanges, 2, 16);
    }

    [Fact]
    public async Task NewerNavigationPreventsStaleResultFromOverwritingScene()
    {
        var root = Normalize("stale-root");
        var folderA = Path.Combine(root, "A");
        var folderB = Path.Combine(root, "B");
        var provider = new ControllableProvider(root, Snapshot(root,
            Entry(folderA, ExplorerNodeKind.Folder),
            Entry(folderB, ExplorerNodeKind.Folder)));
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        var navigateA = session.NavigateAsync(folderA);
        await provider.WaitForRequestAsync(folderA);
        var navigateB = session.NavigateAsync(folderB);
        await provider.WaitForRequestAsync(folderB);
        provider.Complete(folderB, Snapshot(folderB, Entry(Path.Combine(folderB, "current.txt"), ExplorerNodeKind.File)));

        Assert.True(await navigateB);
        provider.Complete(folderA, Snapshot(folderA, Entry(Path.Combine(folderA, "stale.txt"), ExplorerNodeKind.File)));
        Assert.False(await navigateA);

        Assert.Equal(folderB, session.CurrentPath);
        Assert.Equal(folderB, session.Neighborhood!.Focus.Path);
        Assert.DoesNotContain(session.Neighborhood.Nodes, node => node.Name == "stale.txt");
    }

    [Fact]
    public async Task ConnectedNavigation_RejectsLateOpaqueResponseAndReportsDiagnostic()
    {
        const string root = "opaque-root";
        const string folderA = "opaque-a";
        const string folderB = "opaque-b";
        var provider = new ControllableProvider(root, Snapshot(root,
            Entry(folderA, ExplorerNodeKind.Folder),
            Entry(folderB, ExplorerNodeKind.Folder)))
        {
            Mode = ExplorerProviderMode.Connected,
        };
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        var navigateA = session.NavigateAsync(folderA);
        await provider.WaitForRequestAsync(folderA);
        var navigateB = session.NavigateAsync(folderB);
        await provider.WaitForRequestAsync(folderB);
        provider.Complete(folderB, Snapshot(folderB, Entry("current", ExplorerNodeKind.File)));
        Assert.True(await navigateB);
        provider.Complete(folderA, Snapshot(folderA, Entry("stale", ExplorerNodeKind.File)));
        Assert.False(await navigateA);

        Assert.Equal(folderB, session.Neighborhood!.FocusNodeId);
        Assert.Equal(1, provider.StaleResponseRejections);
    }

    [Fact]
    public async Task ClearSearch_CancelsAndResetsPresentationState()
    {
        var root = Normalize("clear-search");
        var provider = new FakeProvider(root, [Snapshot(root)]);
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        await session.SearchAsync("anything");
        session.ClearSearch();

        Assert.Null(session.SearchResult);
        Assert.Empty(session.HighlightedNodeIds);
        Assert.Empty(session.SearchQuery);
        Assert.False(session.IsSearching);
    }

    private static string Normalize(string name) =>
        Path.Combine(Path.GetTempPath(), $"OmniBrilleSessionTests-{name}");

    private static ExplorerEntry PreviewEntry(string id, string name, string? parent = null) =>
        new(id, name, $"Authorized / {name}", ExplorerNodeKind.Folder,
            NavigationTarget: id, ParentNavigationTarget: parent);

    private static ExplorerEntry Entry(string path, ExplorerNodeKind kind) =>
        new(path, Path.GetFileName(path), path, kind);

    private static ExplorerEntry EntryWithParent(string path, string? parent) =>
        new(path, Path.GetFileName(path), path, ExplorerNodeKind.Folder, ParentNavigationTarget: parent);

    private static ExplorerDirectorySnapshot Snapshot(string path, params ExplorerEntry[] children) =>
        new(Entry(path, ExplorerNodeKind.Folder), children);

    private sealed class PreviewProvider : IExplorerProvider, IExplorerSearchProvider, IExplorerDirectoryPreviewProvider, IExplorerContextProvider
    {
        public PreviewProvider(ExplorerDirectorySnapshot root)
        {
            AccessRoot = root.Focus.Target;
            Directories[AccessRoot] = root;
        }

        public string AccessRoot { get; }

        public ExplorerProviderMode Mode => ExplorerProviderMode.Connected;

        public Dictionary<string, ExplorerDirectorySnapshot> Directories { get; } = new(ExplorerIdentity.Comparer);

        public Dictionary<string, ExplorerDirectorySnapshot> Previews { get; } = new(ExplorerIdentity.Comparer);

        public List<string> PreviewRequests { get; } = [];

        public string? FailingTarget { get; set; }

        public string? DeferredTarget { get; init; }

        public TaskCompletionSource<ExplorerDirectorySnapshot> DeferredPreview { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(Directories[path]);

        public Task<ExplorerDirectorySnapshot> GetDirectoryPreviewAsync(string target, CancellationToken cancellationToken = default)
        {
            PreviewRequests.Add(target);
            if (target == FailingTarget)
            {
                throw new IOException("Preview unavailable.");
            }

            return target == DeferredTarget
                ? DeferredPreview.Task
                : Task.FromResult(Previews[target]);
        }

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult([], false, 0));

        public Task<ExplorerContextSnapshot> GetContextAsync(string nodeId, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerContextSnapshot(
                Directories[AccessRoot].Children.Single(child => child.Id == nodeId), [], [], []));
    }

    private sealed class FakeProvider : IExplorerProvider, IExplorerSearchProvider
    {
        private readonly Dictionary<string, ExplorerDirectorySnapshot> _snapshots;

        public FakeProvider(string root, IEnumerable<ExplorerDirectorySnapshot> snapshots)
        {
            AccessRoot = root;
            _snapshots = snapshots.ToDictionary(snapshot => snapshot.Focus.Path, StringComparer.OrdinalIgnoreCase);
        }

        public string AccessRoot { get; }

        public ExplorerSearchResult SearchResult { get; init; } = new([], false, 0);

        public void ReplaceSnapshot(ExplorerDirectorySnapshot snapshot) => _snapshots[snapshot.Focus.Path] = snapshot;

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_snapshots[path]);
        }

        public Task<ExplorerSearchResult> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SearchResult);
        }
    }

    private sealed class ProgressiveFakeProvider :
        IExplorerProvider,
        IProgressiveExplorerProvider,
        IExplorerSearchProvider
    {
        private readonly System.Threading.Channels.Channel<ExplorerDirectoryBatch> _batches =
            System.Threading.Channels.Channel.CreateUnbounded<ExplorerDirectoryBatch>();

        public ProgressiveFakeProvider(string root)
        {
            AccessRoot = root;
        }

        public string AccessRoot { get; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ExplorerDirectoryBatch> GetDirectoryBatchesAsync(
            string path,
            int batchSize,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new ExplorerDirectoryBatch(Entry(path, ExplorerNodeKind.Folder), [], 0, false);
            Started.TrySetResult();
            await foreach (var batch in _batches.Reader.ReadAllAsync(cancellationToken))
            {
                yield return batch;
                if (batch.IsComplete)
                {
                    yield break;
                }
            }
        }

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult([], false, 0));

        public void Publish(IReadOnlyList<ExplorerEntry> entries, bool isComplete)
        {
            _batches.Writer.TryWrite(new ExplorerDirectoryBatch(
                Entry(AccessRoot, ExplorerNodeKind.Folder),
                entries,
                entries.Count,
                isComplete,
                TotalChildCount: entries.Count));
            if (isComplete)
            {
                _batches.Writer.TryComplete();
            }
        }
    }

    private sealed class ControllableProvider :
        IExplorerProvider,
        IExplorerSearchProvider,
        IExplorerProviderDiagnostics
    {
        private readonly ExplorerDirectorySnapshot _rootSnapshot;
        private readonly Dictionary<string, TaskCompletionSource<ExplorerDirectorySnapshot>> _requests =
            new(StringComparer.OrdinalIgnoreCase);

        public ControllableProvider(string root, ExplorerDirectorySnapshot rootSnapshot)
        {
            AccessRoot = root;
            _rootSnapshot = rootSnapshot;
        }

        public string AccessRoot { get; }

        public ExplorerProviderMode Mode { get; init; }

        public int StaleResponseRejections { get; private set; }

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(path, AccessRoot))
            {
                return Task.FromResult(_rootSnapshot);
            }

            lock (_requests)
            {
                if (!_requests.TryGetValue(path, out var request))
                {
                    request = new TaskCompletionSource<ExplorerDirectorySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _requests[path] = request;
                }

                return request.Task;
            }
        }

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult([], false, 0));

        public async Task WaitForRequestAsync(string path)
        {
            await WaitUntilAsync(() =>
            {
                lock (_requests)
                {
                    return _requests.ContainsKey(path);
                }
            });
        }

        public void Complete(string path, ExplorerDirectorySnapshot snapshot)
        {
            lock (_requests)
            {
                _requests[path].TrySetResult(snapshot);
            }
        }

        public void Defer(string path)
        {
            lock (_requests)
            {
                _requests[path] = new TaskCompletionSource<ExplorerDirectorySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public void Fail(string path)
        {
            lock (_requests)
            {
                _requests[path].TrySetException(new IOException("Delayed provider failure."));
            }
        }

        public void ReportStaleResponseRejected() => StaleResponseRejections++;
    }

    private sealed class BurstProgressiveProvider :
        IExplorerProvider,
        IProgressiveExplorerProvider,
        IExplorerSearchProvider
    {
        private readonly int _itemCount;

        public BurstProgressiveProvider(string root, int itemCount)
        {
            AccessRoot = root;
            _itemCount = itemCount;
        }

        public string AccessRoot { get; }

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ExplorerDirectoryBatch> GetDirectoryBatchesAsync(
            string path,
            int batchSize,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var observed = 0;
            while (observed < _itemCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(batchSize, _itemCount - observed);
                var entries = Enumerable.Range(observed, count)
                    .Select(index => Entry(Path.Combine(path, $"item-{index:D5}.txt"), ExplorerNodeKind.File))
                    .ToArray();
                observed += count;
                yield return new ExplorerDirectoryBatch(
                    Entry(path, ExplorerNodeKind.Folder),
                    entries,
                    observed,
                    observed == _itemCount,
                    TotalChildCount: _itemCount);
                await Task.Yield();
            }
        }

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult([], false, 0));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "The expected asynchronous state was not reached before the test deadline.");
    }

    private static async Task ObserveSessionStateAsync(ExplorerSession session, Func<bool> condition)
    {
        if (condition())
        {
            return;
        }

        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnStateChanged(object? sender, EventArgs args)
        {
            if (condition())
            {
                observed.TrySetResult();
            }
        }

        session.StateChanged += OnStateChanged;
        try
        {
            if (condition())
            {
                return;
            }

            await observed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            session.StateChanged -= OnStateChanged;
        }
    }
}
