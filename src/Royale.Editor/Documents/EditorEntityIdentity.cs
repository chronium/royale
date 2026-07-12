namespace Royale.Editor.Documents;

public enum EditorEntityKind { StaticBox, StaticModel, SpawnPoint, LootPoint, NavigationWaypoint, NavigationLink }

public readonly record struct EditorEntityIdentity(Guid EditorId, EditorEntityKind Kind, int Index);
