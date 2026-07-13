using System.Text.Json.Serialization;

namespace Royale.Editor.Mcp;

public sealed record EditorMcpVector3(float X, float Y, float Z);

public sealed record EditorMcpBounds(EditorMcpVector3 Min, EditorMcpVector3 Max);

public sealed record EditorMcpSafeZone(EditorMcpVector3 Center, float Radius);

public sealed record EditorMcpTransform(
    EditorMcpVector3 Position,
    EditorMcpVector3 RotationDegrees,
    EditorMcpVector3 ScaleOrSize);

public sealed record EditorMcpTransformCapabilities(bool Translate, bool Rotate, bool Scale);

public sealed record EditorMcpSelection(Guid EditorId, string Kind, string DisplayId);

public sealed record EditorMcpStatusResult(
    string DocumentType,
    string? Path,
    string MapId,
    string MapName,
    long Revision,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    string? UndoDescription,
    string? RedoDescription,
    EditorMcpSelection? Selection,
    string? ManifestFingerprint);

public sealed record EditorMcpEntityCounts(
    int StaticBoxes,
    int StaticModels,
    int SpawnPoints,
    int LootPoints,
    int NavigationWaypoints,
    int NavigationLinks);

public sealed record EditorMcpMapResult(
    string Name,
    string Id,
    EditorMcpBounds WorldBounds,
    EditorMcpSafeZone SafeZone,
    EditorMcpEntityCounts EntityCounts,
    long Revision);

public sealed record EditorMcpAssetResult(
    string Id,
    string? RenderSource,
    IReadOnlyList<string> RenderResources,
    string CollisionMode,
    string? CollisionSource,
    string? CollisionArtifact,
    bool RenderCapable);

public sealed record EditorMcpAssetListResult(
    IReadOnlyList<EditorMcpAssetResult> Assets,
    string? ManifestFingerprint,
    long Revision);

public sealed record EditorMcpEntitySummary(Guid EditorId, string Kind, string DisplayId, int Index);

public sealed record EditorMcpEntityListResult(IReadOnlyList<EditorMcpEntitySummary> Entities, long Revision);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(EditorMcpStaticBoxDefinition), "staticBox")]
[JsonDerivedType(typeof(EditorMcpStaticModelDefinition), "staticModel")]
[JsonDerivedType(typeof(EditorMcpSpawnPointDefinition), "spawnPoint")]
[JsonDerivedType(typeof(EditorMcpLootPointDefinition), "lootPoint")]
[JsonDerivedType(typeof(EditorMcpNavigationWaypointDefinition), "navigationWaypoint")]
[JsonDerivedType(typeof(EditorMcpNavigationLinkDefinition), "navigationLink")]
public abstract record EditorMcpEntityDefinition;

public sealed record EditorMcpStaticBoxDefinition(
    string Id,
    EditorMcpVector3 Position,
    EditorMcpVector3 RotationDegrees,
    EditorMcpVector3 Size) : EditorMcpEntityDefinition;

public sealed record EditorMcpStaticModelDefinition(
    string Id,
    string AssetId,
    EditorMcpVector3 Position,
    EditorMcpVector3 RotationDegrees,
    EditorMcpVector3 Scale) : EditorMcpEntityDefinition;

public sealed record EditorMcpSpawnPointDefinition(
    string Id,
    EditorMcpVector3 Position,
    EditorMcpVector3 RotationDegrees) : EditorMcpEntityDefinition;

public sealed record EditorMcpLootPointDefinition(
    string Id,
    EditorMcpVector3 Position) : EditorMcpEntityDefinition;

public sealed record EditorMcpNavigationWaypointDefinition(
    string Id,
    EditorMcpVector3 Position) : EditorMcpEntityDefinition;

public sealed record EditorMcpNavigationLinkDefinition(string From, string To) : EditorMcpEntityDefinition;

public sealed record EditorMcpEntityResult(
    Guid EditorId,
    EditorMcpEntityDefinition Definition,
    EditorMcpTransform? Transform,
    EditorMcpTransformCapabilities TransformCapabilities,
    long Revision);

public sealed record EditorMcpValidationStage(string Category, bool Success, string Message);

public sealed record EditorMcpValidationResult(
    bool Success,
    long Revision,
    string? ManifestFingerprint,
    IReadOnlyList<EditorMcpValidationStage> Stages);

public sealed record EditorMcpMutationResult(bool Changed, long Revision);

public sealed record EditorMcpEntityMutationResult(bool Changed, long Revision, Guid EditorId);

public sealed record EditorMcpDeleteResult(bool Changed, long Revision, int RemovedIncidentLinks);

public sealed record EditorMcpSaveResult(bool Saved, string Path, long Revision);

public sealed record EditorMcpFaceSnapResult(
    bool Changed,
    long Revision,
    bool Hit,
    EditorMcpVector3? Point,
    EditorMcpVector3? Normal,
    float? Fraction,
    string? ColliderContentId,
    string? ColliderKind,
    string? ColliderAssetId);

public enum EditorMcpFaceSnapAxis
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ,
}
