using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class ExplorerSceneSemanticsTests
{
    [Fact]
    public void RelationOf_SeparatesNavigationMeaningFromPresentationDensity()
    {
        var focus = Node("root", ExplorerNodeKind.Folder, ExplorerNodeRole.Structural);
        var child = Node("child", ExplorerNodeKind.File, ExplorerNodeRole.Structural);
        var previous = Node("previous", ExplorerNodeKind.Context, ExplorerNodeRole.None);
        var related = Node("related", ExplorerNodeKind.File, ExplorerNodeRole.Contextual);
        var both = Node("both", ExplorerNodeKind.File, ExplorerNodeRole.Structural | ExplorerNodeRole.Contextual);
        var aggregate = Node("aggregate", ExplorerNodeKind.Aggregate, ExplorerNodeRole.Structural);
        var scene = new ExplorerNeighborhood(
            focus.Id,
            [focus, child, previous, related, both, aggregate],
            [],
            5,
            0);

        Assert.Equal(ExplorerSceneRelation.CurrentFocus, ExplorerSceneSemantics.RelationOf(scene, focus));
        Assert.Equal(ExplorerSceneRelation.DirectChild, ExplorerSceneSemantics.RelationOf(scene, child));
        Assert.Equal(ExplorerSceneRelation.PreviousFocus, ExplorerSceneSemantics.RelationOf(scene, previous));
        Assert.Equal(ExplorerSceneRelation.Contextual, ExplorerSceneSemantics.RelationOf(scene, related));
        Assert.Equal(ExplorerSceneRelation.StructuralAndContextual, ExplorerSceneSemantics.RelationOf(scene, both));
        Assert.Equal(ExplorerSceneRelation.Aggregate, ExplorerSceneSemantics.RelationOf(scene, aggregate));
        Assert.Equal("direct child of root", ExplorerSceneSemantics.Describe(scene, child));
    }

    private static ExplorerNode Node(string id, ExplorerNodeKind kind, ExplorerNodeRole roles) =>
        new(id, id, id, kind, null, null, kind == ExplorerNodeKind.Folder, Roles: roles);
}
