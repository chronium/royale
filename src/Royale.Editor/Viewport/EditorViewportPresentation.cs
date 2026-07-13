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
            Matrix4x4 world = transform.CreateMatrix();
            Vector3 minimum;
            Vector3 maximum;
            switch (identity.Kind)
            {
                case EditorEntityKind.StaticBox:
                    minimum = new Vector3(-0.5f);
                    maximum = new Vector3(0.5f);
                    break;
                case EditorEntityKind.StaticModel:
                    (minimum, maximum) = EditorViewportPicking.GetMeshBounds(
                        meshCache.GetRequired(document.Map.StaticModels[identity.Index].AssetId));
                    break;
                case EditorEntityKind.SpawnPoint:
                    minimum = new Vector3(-EditorViewportPicking.SpawnProxyRadius);
                    maximum = new Vector3(EditorViewportPicking.SpawnProxyRadius);
                    world = Matrix4x4.CreateTranslation(transform.Position);
                    AddSpawnMarker(debug, transform);
                    break;
                case EditorEntityKind.LootPoint:
                    minimum = new Vector3(-EditorViewportPicking.LootProxyRadius);
                    maximum = new Vector3(EditorViewportPicking.LootProxyRadius);
                    world = Matrix4x4.CreateTranslation(transform.Position);
                    AddLootMarker(debug, transform.Position);
                    break;
                default:
                    minimum = new Vector3(-EditorViewportPicking.NavigationProxyRadius);
                    maximum = new Vector3(EditorViewportPicking.NavigationProxyRadius);
                    world = Matrix4x4.CreateTranslation(transform.Position);
                    AddNavigationMarker(debug, transform.Position);
                    break;
            }

            var target = new EditorPickTarget(identity, world, minimum, maximum);
            targets.Add(target);
            if (selectedEditorId == identity.EditorId)
                AddSelectionHighlight(debug, target);
        }

        return new EditorViewportPresentation(debug, targets);
    }

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
