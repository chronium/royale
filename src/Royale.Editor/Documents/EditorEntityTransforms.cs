using System.Numerics;
using Royale.Content.Maps;

namespace Royale.Editor.Documents;

public static class EditorEntityTransforms
{
    public static bool HasSpatialTransform(EditorEntityKind kind) =>
        GetCapabilities(kind) != EditorTransformCapabilities.None;

    public static EditorTransformCapabilities GetCapabilities(EditorEntityKind kind) => kind switch
    {
        EditorEntityKind.StaticBox or EditorEntityKind.StaticModel =>
            EditorTransformCapabilities.Translate | EditorTransformCapabilities.Rotate | EditorTransformCapabilities.Scale,
        EditorEntityKind.SpawnPoint =>
            EditorTransformCapabilities.Translate | EditorTransformCapabilities.Rotate,
        EditorEntityKind.LootPoint or EditorEntityKind.NavigationWaypoint =>
            EditorTransformCapabilities.Translate,
        _ => EditorTransformCapabilities.None,
    };

    public static string GetDisplayId(EditorMapDocument document, EditorEntityIdentity identity) => identity.Kind switch
    {
        EditorEntityKind.StaticBox => document.Map.StaticBoxes[identity.Index].Id,
        EditorEntityKind.StaticModel => document.Map.StaticModels[identity.Index].Id,
        EditorEntityKind.SpawnPoint => document.Map.SpawnPoints[identity.Index].Id,
        EditorEntityKind.LootPoint => document.Map.LootPoints[identity.Index].Id,
        EditorEntityKind.NavigationWaypoint => document.Map.Navigation.Waypoints[identity.Index].Id,
        EditorEntityKind.NavigationLink => $"{document.Map.Navigation.Links[identity.Index].From} -> {document.Map.Navigation.Links[identity.Index].To}",
        _ => throw new ArgumentOutOfRangeException(nameof(identity)),
    };

    public static EditorEntityTransform Get(EditorMapDocument document, EditorEntityIdentity identity)
    {
        ValidateIdentity(document, identity);
        return identity.Kind switch
        {
            EditorEntityKind.StaticBox => From(document.Map.StaticBoxes[identity.Index]),
            EditorEntityKind.StaticModel => From(document.Map.StaticModels[identity.Index]),
            EditorEntityKind.SpawnPoint => From(document.Map.SpawnPoints[identity.Index]),
            EditorEntityKind.LootPoint => PositionOnly(document.Map.LootPoints[identity.Index].Position),
            EditorEntityKind.NavigationWaypoint => PositionOnly(document.Map.Navigation.Waypoints[identity.Index].Position),
            _ => throw new InvalidOperationException("Navigation links do not have a spatial transform."),
        };
    }

    public static void Set(
        EditorMapDocument document,
        EditorEntityIdentity identity,
        EditorEntityTransform transform,
        bool validate = true)
    {
        ValidateIdentity(document, identity);
        if (validate)
            ValidateTransform(identity.Kind, transform);
        GameMap map = document.Map;
        switch (identity.Kind)
        {
            case EditorEntityKind.StaticBox:
                map.StaticBoxes[identity.Index] = map.StaticBoxes[identity.Index] with
                {
                    Position = EditorEntityTransform.ToMap(transform.Position),
                    RotationEuler = EditorEntityTransform.ToMap(transform.RotationDegrees),
                    Size = EditorEntityTransform.ToMap(transform.ScaleOrSize),
                };
                break;
            case EditorEntityKind.StaticModel:
                map.StaticModels[identity.Index] = map.StaticModels[identity.Index] with
                {
                    Position = EditorEntityTransform.ToMap(transform.Position),
                    RotationEuler = EditorEntityTransform.ToMap(transform.RotationDegrees),
                    Scale = EditorEntityTransform.ToMap(transform.ScaleOrSize),
                };
                break;
            case EditorEntityKind.SpawnPoint:
                map.SpawnPoints[identity.Index] = map.SpawnPoints[identity.Index] with
                {
                    Position = EditorEntityTransform.ToMap(transform.Position),
                    RotationEuler = EditorEntityTransform.ToMap(transform.RotationDegrees),
                };
                break;
            case EditorEntityKind.LootPoint:
                map.LootPoints[identity.Index] = map.LootPoints[identity.Index] with
                {
                    Position = EditorEntityTransform.ToMap(transform.Position),
                };
                break;
            case EditorEntityKind.NavigationWaypoint:
                map.Navigation.Waypoints[identity.Index] = map.Navigation.Waypoints[identity.Index] with
                {
                    Position = EditorEntityTransform.ToMap(transform.Position),
                };
                break;
            default:
                throw new InvalidOperationException("Navigation links do not have a spatial transform.");
        }
    }

    public static void ValidateTransform(EditorEntityKind kind, EditorEntityTransform transform)
    {
        if (!transform.IsFinite)
            throw new ArgumentException("Entity transforms must contain only finite values.", nameof(transform));
        if (kind == EditorEntityKind.StaticBox &&
            (transform.ScaleOrSize.X <= 0.0f || transform.ScaleOrSize.Y <= 0.0f || transform.ScaleOrSize.Z <= 0.0f))
            throw new ArgumentException("Static box sizes must be positive.", nameof(transform));
        if (kind == EditorEntityKind.StaticModel &&
            (transform.ScaleOrSize.X == 0.0f || transform.ScaleOrSize.Y == 0.0f || transform.ScaleOrSize.Z == 0.0f))
            throw new ArgumentException("Static model scale cannot contain zero.", nameof(transform));
    }

    public static void ValidateIdentity(EditorMapDocument document, EditorEntityIdentity identity)
    {
        if (!document.Identities.Contains(identity))
            throw new KeyNotFoundException($"Editor entity '{identity.EditorId}' does not belong to this document.");
    }

    private static EditorEntityTransform From(StaticBoxDefinition value) => new(
        ToVector(value.Position), ToVector(value.RotationEuler), ToVector(value.Size));

    private static EditorEntityTransform From(StaticModelDefinition value) => new(
        ToVector(value.Position), ToVector(value.RotationEuler), ToVector(value.Scale));

    private static EditorEntityTransform From(MapSpawnPoint value) => new(
        ToVector(value.Position), ToVector(value.RotationEuler), Vector3.One);

    private static EditorEntityTransform PositionOnly(MapVector3 value) =>
        new(ToVector(value), Vector3.Zero, Vector3.One);

    private static Vector3 ToVector(MapVector3 value) => new(value.X, value.Y, value.Z);
}
