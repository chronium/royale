using System.Numerics;
using Royale.Editor.Documents;
using Royale.Rendering.Debug;
using Royale.Rendering.Meshes;

namespace Royale.Editor.Viewport;

public sealed record EditorViewportPresentation(
    DebugPrimitiveList DebugPrimitives,
    IReadOnlyList<EditorPickTarget> PickTargets);

public static class EditorViewportPresentationBuilder
{
    private static readonly Vector4 GridMinor = new(0.24f, 0.27f, 0.32f, 0.45f);
    private static readonly Vector4 GridMajor = new(0.38f, 0.42f, 0.49f, 0.65f);
    private static readonly Vector4 AxisX = new(0.85f, 0.18f, 0.18f, 0.9f);
    private static readonly Vector4 AxisZ = new(0.20f, 0.42f, 0.90f, 0.9f);
    private static readonly Vector4 SpawnColor = new(0.20f, 0.80f, 0.95f, 0.9f);
    private static readonly Vector4 LootColor = new(0.95f, 0.78f, 0.18f, 0.9f);
    private static readonly Vector4 NavigationColor = new(0.35f, 0.90f, 0.40f, 0.9f);
    private static readonly Vector4 SelectionColor = new(1.0f, 0.55f, 0.10f, 1.0f);

    public static EditorViewportPresentation Build(
        EditorMapDocument document,
        StaticMeshAssetCache meshCache,
        Guid? selectedEditorId,
        bool gridVisible,
        float gridSpacing)
    {
        var debug = new DebugPrimitiveList();
        var targets = new List<EditorPickTarget>();

        if (gridVisible)
        {
            EditorGrid grid = EditorGridGenerator.Generate(document.Map.WorldBounds, gridSpacing);
            foreach (EditorGridLine line in grid.Lines)
                debug.AddLine(line.Start, line.End, GridColor(line.Kind));
        }

        foreach (EditorEntityIdentity identity in document.Identities)
        {
            if (identity.Kind == EditorEntityKind.NavigationLink)
                continue;

            EditorEntityTransform transform = EditorEntityTransforms.Get(document, identity);
            EditorPickTarget target = CreatePickTarget(document, meshCache, identity);
            switch (identity.Kind)
            {
                case EditorEntityKind.SpawnPoint:
                    AddSpawnMarker(debug, transform);
                    break;
                case EditorEntityKind.LootPoint:
                    AddLootMarker(debug, transform.Position);
                    break;
                case EditorEntityKind.NavigationWaypoint:
                    AddNavigationMarker(debug, transform.Position);
                    break;
            }

            targets.Add(target);
            if (selectedEditorId == identity.EditorId)
                AddSelectionHighlight(debug, target);
        }

        return new EditorViewportPresentation(debug, targets);
    }

    public static EditorPickTarget CreatePickTarget(
        EditorMapDocument document,
        StaticMeshAssetCache meshCache,
        EditorEntityIdentity identity)
    {
        EditorEntityTransform transform = EditorEntityTransforms.Get(document, identity);
        return identity.Kind switch
        {
            EditorEntityKind.StaticBox => new EditorPickTarget(
                identity,
                transform.CreateMatrix(),
                new Vector3(-0.5f),
                new Vector3(0.5f)),
            EditorEntityKind.StaticModel => CreateModelTarget(document, meshCache, identity, transform),
            EditorEntityKind.SpawnPoint => CreateProxyTarget(identity, transform.Position, EditorViewportPicking.SpawnProxyRadius),
            EditorEntityKind.LootPoint => CreateProxyTarget(identity, transform.Position, EditorViewportPicking.LootProxyRadius),
            EditorEntityKind.NavigationWaypoint => CreateProxyTarget(identity, transform.Position, EditorViewportPicking.NavigationProxyRadius),
            _ => throw new InvalidOperationException("Navigation links do not have picking bounds."),
        };
    }

    private static EditorPickTarget CreateModelTarget(
        EditorMapDocument document,
        StaticMeshAssetCache meshCache,
        EditorEntityIdentity identity,
        EditorEntityTransform transform)
    {
        (Vector3 minimum, Vector3 maximum) = EditorViewportPicking.GetMeshBounds(
            meshCache.GetRequired(document.Map.StaticModels[identity.Index].AssetId));
        return new EditorPickTarget(identity, transform.CreateMatrix(), minimum, maximum);
    }

    private static EditorPickTarget CreateProxyTarget(
        EditorEntityIdentity identity,
        Vector3 position,
        float radius) => new(
            identity,
            Matrix4x4.CreateTranslation(position),
            new Vector3(-radius),
            new Vector3(radius));

    private static void AddSpawnMarker(DebugPrimitiveList debug, EditorEntityTransform transform)
    {
        Vector3 position = transform.Position;
        debug.AddCircleXz(position, 0.45f, SpawnColor, 24);
        debug.AddLine(position, position + Vector3.UnitY * 1.1f, SpawnColor);
        Vector3 forward = Vector3.TransformNormal(-Vector3.UnitZ, Matrix4x4.CreateRotationY(transform.RotationDegrees.Y * MathF.PI / 180.0f));
        debug.AddLine(position + Vector3.UnitY * 0.55f, position + Vector3.UnitY * 0.55f + forward * 0.75f, SpawnColor);
    }

    private static void AddLootMarker(DebugPrimitiveList debug, Vector3 position)
    {
        float radius = EditorViewportPicking.LootProxyRadius;
        debug.AddLine(position + Vector3.UnitY * radius, position + Vector3.UnitX * radius, LootColor);
        debug.AddLine(position + Vector3.UnitX * radius, position - Vector3.UnitY * radius, LootColor);
        debug.AddLine(position - Vector3.UnitY * radius, position - Vector3.UnitX * radius, LootColor);
        debug.AddLine(position - Vector3.UnitX * radius, position + Vector3.UnitY * radius, LootColor);
        debug.AddLine(position + Vector3.UnitY * radius, position + Vector3.UnitZ * radius, LootColor);
        debug.AddLine(position + Vector3.UnitZ * radius, position - Vector3.UnitY * radius, LootColor);
        debug.AddLine(position - Vector3.UnitY * radius, position - Vector3.UnitZ * radius, LootColor);
        debug.AddLine(position - Vector3.UnitZ * radius, position + Vector3.UnitY * radius, LootColor);
    }

    private static void AddNavigationMarker(DebugPrimitiveList debug, Vector3 position)
    {
        debug.AddCircleXz(position, EditorViewportPicking.NavigationProxyRadius, NavigationColor, 16);
        debug.AddPoint(position, EditorViewportPicking.NavigationProxyRadius * 1.5f, NavigationColor);
    }

    private static void AddSelectionHighlight(DebugPrimitiveList debug, EditorPickTarget target)
    {
        Vector3 center = (target.LocalMinimum + target.LocalMaximum) * 0.5f;
        Vector3 extents = (target.LocalMaximum - target.LocalMinimum) * 0.52f;
        debug.AddWireBox(extents, Matrix4x4.CreateTranslation(center) * target.Transform, SelectionColor);
    }

    private static Vector4 GridColor(EditorGridLineKind kind) => kind switch
    {
        EditorGridLineKind.Major => GridMajor,
        EditorGridLineKind.AxisX => AxisX,
        EditorGridLineKind.AxisZ => AxisZ,
        _ => GridMinor,
    };
}
