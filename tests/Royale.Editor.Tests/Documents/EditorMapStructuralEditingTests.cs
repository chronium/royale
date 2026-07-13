using Royale.Content.Maps;
using Royale.Editor.Documents;
using Royale.Editor.Workspace;

namespace Royale.Editor.Tests.Documents;

public sealed class EditorMapStructuralEditingTests
{
    [Fact]
    public void AddRemoveUndoRedoReindexesAndPreservesEditorIds()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity original = document.Identities.Single(value => value.Kind == EditorEntityKind.StaticBox);
        Guid addedId = Guid.NewGuid();
        var added = new StaticBoxDefinition { Id = "second", Size = new MapVector3(1, 2, 3) };

        document.Execute(new AddEntityCommand(EditorEntityKind.StaticBox, 0, added, addedId));
        Assert.Equal(0, document.GetIdentity(addedId).Index);
        Assert.Equal(1, document.GetIdentity(original.EditorId).Index);

        Assert.True(document.Undo());
        Assert.False(document.TryGetIdentity(addedId, out _));
        Assert.Equal(0, document.GetIdentity(original.EditorId).Index);

        Assert.True(document.Redo());
        Assert.Equal(0, document.GetIdentity(addedId).Index);
        Assert.Equal(original.EditorId, document.Identities.Single(value => value.Kind == EditorEntityKind.StaticBox && value.Index == 1).EditorId);

        document.Execute(new RemoveEntityCommand(original.EditorId));
        Assert.False(document.TryGetIdentity(original.EditorId, out _));
        document.Undo();
        Assert.Equal(1, document.GetIdentity(original.EditorId).Index);
    }

    [Fact]
    public void UniqueIdsShareStaticNamespaceAndUseNumberedSuffixes()
    {
        EditorMapDocument document = CreateDocument().WithEntities(
            models: [new StaticModelDefinition { Id = "crate-2", AssetId = "crate" }]);

        Assert.Equal("crate-3", EditorMapEditing.CreateUniqueId(document, EditorEntityKind.StaticModel, "Crate"));
        Assert.Equal("new-asset", EditorMapEditing.CreateUniqueId(document, EditorEntityKind.StaticModel, "New Asset"));
    }

    [Fact]
    public void DuplicateCopiesEveryPropertyExceptRuntimeAndEditorIds()
    {
        var model = new StaticModelDefinition
        {
            Id = "crate",
            AssetId = "crate-asset",
            Position = new MapVector3(1, 2, 3),
            RotationEuler = new MapVector3(4, 5, 6),
            Scale = new MapVector3(7, 8, 9),
        };
        EditorMapDocument document = CreateDocument().WithEntities(models: [model]);
        EditorEntityIdentity identity = document.Identities.Single(value => value.Kind == EditorEntityKind.StaticModel);

        var duplicate = Assert.IsType<StaticModelDefinition>(EditorMapEditing.DuplicateDefinition(document, identity));

        Assert.Equal(model with { Id = "crate-2" }, duplicate);
        Assert.Throws<InvalidOperationException>(() => EditorMapEditing.DuplicateDefinition(
            document,
            document.Identities.Single(value => value.Kind == EditorEntityKind.NavigationLink)));
    }

    [Fact]
    public void EntityAndRootPropertyCommandsRoundTrip()
    {
        EditorMapDocument document = CreateDocument();
        foreach (EditorEntityIdentity identity in document.Identities)
        {
            object before = document.GetForTest(identity.EditorId);
            object after = before switch
            {
                StaticBoxDefinition value => value with { Position = new MapVector3(2, 0, 0) },
                StaticModelDefinition value => value with { AssetId = "other" },
                MapSpawnPoint value => value with { RotationEuler = new MapVector3(0, 90, 0) },
                MapLootPoint value => value with { Position = new MapVector3(1, 0, 0) },
                MapNavigationWaypoint value => value with { Position = new MapVector3(0, 0, 1) },
                MapNavigationLink value => value with { From = value.To, To = value.From },
                _ => throw new InvalidOperationException(),
            };
            document.Execute(new ReplaceEntityCommand(identity.EditorId, before, after));
            Assert.Equal(after, document.GetForTest(identity.EditorId));
            document.Undo();
            Assert.Equal(before, document.GetForTest(identity.EditorId));
        }

        var bounds = new MapBounds { Min = new MapVector3(10, 0, 0), Max = new MapVector3(-10, 0, 0) };
        document.Execute(new SetWorldBoundsCommand(document.Map.WorldBounds, bounds));
        Assert.Equal(bounds, document.Map.WorldBounds);
        var zone = new SafeZoneDefinition { Center = new MapVector3(2, 0, 2), Radius = 3 };
        document.Execute(new SetSafeZoneCommand(document.Map.SafeZone, zone));
        Assert.Equal(zone, document.Map.SafeZone);
    }

    [Fact]
    public void WaypointRenameAndDeleteCascadeAndUndoExactly()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity waypoint = document.Identities.Single(value =>
            value.Kind == EditorEntityKind.NavigationWaypoint && value.Index == 0);
        EditorEntityIdentity link = document.Identities.Single(value => value.Kind == EditorEntityKind.NavigationLink);

        document.Execute(new RenameWaypointCommand(waypoint.EditorId, "a", "renamed"));
        Assert.Equal("renamed", document.Map.Navigation.Links[0].From);
        document.Undo();
        Assert.Equal("a", document.Map.Navigation.Links[0].From);

        var remove = new RemoveEntityCommand(waypoint.EditorId);
        document.Execute(remove);
        Assert.Equal(1, remove.IncidentLinkCount);
        Assert.Empty(document.Map.Navigation.Links);
        document.Undo();
        Assert.Equal(waypoint.EditorId, document.GetIdentity(waypoint.EditorId).EditorId);
        Assert.Equal(link.EditorId, document.GetIdentity(link.EditorId).EditorId);
        Assert.Equal("a", document.Map.Navigation.Waypoints[0].Id);
        Assert.Equal(new MapNavigationLink { From = "a", To = "b" }, document.Map.Navigation.Links[0]);
    }

    [Fact]
    public void InvalidPropertiesAndLinksDoNotEnterHistory()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity box = document.Identities.Single(value => value.Kind == EditorEntityKind.StaticBox);
        StaticBoxDefinition original = document.Map.StaticBoxes[0];

        Assert.Throws<ArgumentException>(() => document.Execute(new ReplaceEntityCommand(
            box.EditorId,
            original,
            original with { Size = new MapVector3(0, 1, 1) })));
        Assert.False(document.CanUndo);
        Assert.Throws<ArgumentException>(() => document.Execute(new AddEntityCommand(
            EditorEntityKind.NavigationLink,
            1,
            new MapNavigationLink { From = "b", To = "a" })));
        Assert.False(document.CanUndo);
        Assert.Throws<ArgumentException>(() => document.Execute(new AddEntityCommand(
            EditorEntityKind.NavigationLink,
            1,
            new MapNavigationLink { From = "a", To = "a" })));
        Assert.False(document.CanUndo);
    }

    [Fact]
    public void LinksCanBeSelectedButHaveNoTransformCapabilities()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity link = document.Identities.Single(value => value.Kind == EditorEntityKind.NavigationLink);
        var selection = new EditorSelectionState();

        selection.Select(document, link.EditorId);

        Assert.Equal(link, selection.Resolve(document));
        Assert.Equal(EditorTransformCapabilities.None, EditorEntityTransforms.GetCapabilities(link.Kind));
        Assert.False(EditorEntityTransforms.HasSpatialTransform(link.Kind));
    }

    [Fact]
    public void StableSelectionResolvesAgainAfterDeleteUndo()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity loot = document.Identities.Single(value => value.Kind == EditorEntityKind.LootPoint);
        var selection = new EditorSelectionState();
        selection.Select(document, loot.EditorId);

        document.Execute(new RemoveEntityCommand(loot.EditorId));
        Assert.Null(selection.Resolve(document));

        document.Undo();
        Assert.Equal(loot.EditorId, selection.Resolve(document)?.EditorId);
    }

    private static EditorMapDocument CreateDocument() => new(new GameMap
    {
        Id = "map",
        Name = "Map",
        StaticBoxes = [new StaticBoxDefinition { Id = "crate", Size = new MapVector3(1, 1, 1) }],
        StaticModels = [new StaticModelDefinition { Id = "model", AssetId = "asset" }],
        SpawnPoints = [new MapSpawnPoint { Id = "spawn" }],
        LootPoints = [new MapLootPoint { Id = "loot" }],
        Navigation = new MapNavigationDefinition
        {
            Waypoints = [new MapNavigationWaypoint { Id = "a" }, new MapNavigationWaypoint { Id = "b" }],
            Links = [new MapNavigationLink { From = "a", To = "b" }],
        },
        WorldBounds = new MapBounds { Min = new MapVector3(-10, -1, -10), Max = new MapVector3(10, 5, 10) },
        SafeZone = new SafeZoneDefinition { Radius = 5 },
    }, null, null, false);
}

internal static class EditorMapDocumentTestExtensions
{
    public static EditorMapDocument WithEntities(
        this EditorMapDocument source,
        List<StaticModelDefinition>? models = null)
    {
        GameMap map = source.Map with { StaticModels = models ?? source.Map.StaticModels };
        return new EditorMapDocument(map, null, null, false);
    }

    public static object GetForTest(this EditorMapDocument document, Guid editorId)
    {
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        return identity.Kind switch
        {
            EditorEntityKind.StaticBox => document.Map.StaticBoxes[identity.Index],
            EditorEntityKind.StaticModel => document.Map.StaticModels[identity.Index],
            EditorEntityKind.SpawnPoint => document.Map.SpawnPoints[identity.Index],
            EditorEntityKind.LootPoint => document.Map.LootPoints[identity.Index],
            EditorEntityKind.NavigationWaypoint => document.Map.Navigation.Waypoints[identity.Index],
            EditorEntityKind.NavigationLink => document.Map.Navigation.Links[identity.Index],
            _ => throw new InvalidOperationException(),
        };
    }
}
