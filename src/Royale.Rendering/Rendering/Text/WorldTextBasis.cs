using System.Numerics;

namespace Royale.Rendering.Text;

public readonly record struct WorldTextBasis(Vector3 Right, Vector3 Up)
{
    public static readonly WorldTextBasis Identity = new(Vector3.UnitX, Vector3.UnitY);

    public bool IsFinite =>
        IsFiniteVector(Right) &&
        IsFiniteVector(Up);

    private static bool IsFiniteVector(Vector3 vector) =>
        float.IsFinite(vector.X) &&
        float.IsFinite(vector.Y) &&
        float.IsFinite(vector.Z);
}
