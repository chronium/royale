using System.Numerics;
using Royale.Rendering.Debug;
using Royale.Simulation.World;

namespace Royale.Editor.Viewport.FaceSnap;

public static class EditorFaceSnapDebug
{
    private static readonly Vector4 HitColor = new(1.0f, 0.25f, 0.75f, 1.0f);
    private static readonly Vector4 NormalColor = new(0.25f, 1.0f, 0.75f, 1.0f);
    private static readonly Vector4 PlaneColor = new(1.0f, 0.85f, 0.20f, 0.9f);

    public static void Add(DebugPrimitiveList debug, MapStaticRayHit hit)
    {
        Vector3 normal = Vector3.Normalize(hit.Normal);
        Vector3 reference = MathF.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(reference, normal)) * 0.35f;
        Vector3 bitangent = Vector3.Normalize(Vector3.Cross(normal, tangent)) * 0.35f;
        debug.AddPoint(hit.Point, 0.16f, HitColor);
        debug.AddLine(hit.Point, hit.Point + normal * 0.75f, NormalColor);
        debug.AddLine(hit.Point - tangent - bitangent, hit.Point + tangent - bitangent, PlaneColor);
        debug.AddLine(hit.Point + tangent - bitangent, hit.Point + tangent + bitangent, PlaneColor);
        debug.AddLine(hit.Point + tangent + bitangent, hit.Point - tangent + bitangent, PlaneColor);
        debug.AddLine(hit.Point - tangent + bitangent, hit.Point - tangent - bitangent, PlaneColor);
    }
}
