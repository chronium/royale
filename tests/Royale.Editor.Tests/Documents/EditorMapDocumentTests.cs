using Royale.Content.Maps;
using Royale.Editor.Documents;

namespace Royale.Editor.Tests.Documents;

public sealed class EditorMapDocumentTests
{
    [Fact]
    public void CommandsTrackDirtyCheckpointUndoRedoAndBranches()
    {
        var document = new EditorMapDocument(new GameMap { Id = "map", Name = "Before" }, null, null, false);
        document.Execute(new SetMapNameCommand("Before", "After"));
        Assert.True(document.IsDirty);
        Assert.Equal(1, document.Revision);
        Assert.True(document.CanUndo);

        document.MarkSaved("map.json", "hash");
        Assert.False(document.IsDirty);

        Assert.True(document.Undo());
        Assert.True(document.IsDirty);
        Assert.Equal("Before", document.Map.Name);

        Assert.True(document.Redo());
        Assert.False(document.IsDirty);
        Assert.Equal("After", document.Map.Name);

        document.Undo();
        document.Execute(new SetMapNameCommand("Before", "Branch"));
        Assert.False(document.CanRedo);
        Assert.True(document.IsDirty);
        Assert.Equal(5, document.Revision);
    }

    [Fact]
    public void CreatesStableUniqueIdentitiesIncludingLinks()
    {
        var map = new GameMap
        {
            Navigation = new MapNavigationDefinition { Links = [new MapNavigationLink()] },
            StaticBoxes = [new StaticBoxDefinition()],
        };
        var document = new EditorMapDocument(map, null, null, false);
        Assert.Equal(2, document.Identities.Count);
        Assert.Equal(2, document.Identities.Select(x => x.EditorId).Distinct().Count());
        Assert.Contains(document.Identities, x => x.Kind == EditorEntityKind.NavigationLink);
    }

    [Fact]
    public void RejectsBlankMapName() =>
        Assert.Throws<ArgumentException>(() => new SetMapNameCommand("Before", "  "));
}
