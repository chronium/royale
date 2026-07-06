using System.Numerics;

namespace Royale.Client.Rendering.Meshes;

public static class UnitBoxMesh
{
    public static StaticMeshGeometry Create()
    {
        return new StaticMeshGeometry(Vertices, Indices);
    }

    private static readonly StaticMeshVertex[] Vertices =
    [
        new(new Vector3(-0.5f, -0.5f, -0.5f), -Vector3.UnitZ),
        new(new Vector3(0.5f, -0.5f, -0.5f), -Vector3.UnitZ),
        new(new Vector3(0.5f, 0.5f, -0.5f), -Vector3.UnitZ),
        new(new Vector3(-0.5f, 0.5f, -0.5f), -Vector3.UnitZ),

        new(new Vector3(-0.5f, -0.5f, 0.5f), Vector3.UnitZ),
        new(new Vector3(0.5f, -0.5f, 0.5f), Vector3.UnitZ),
        new(new Vector3(0.5f, 0.5f, 0.5f), Vector3.UnitZ),
        new(new Vector3(-0.5f, 0.5f, 0.5f), Vector3.UnitZ),

        new(new Vector3(-0.5f, -0.5f, -0.5f), -Vector3.UnitY),
        new(new Vector3(0.5f, -0.5f, -0.5f), -Vector3.UnitY),
        new(new Vector3(0.5f, -0.5f, 0.5f), -Vector3.UnitY),
        new(new Vector3(-0.5f, -0.5f, 0.5f), -Vector3.UnitY),

        new(new Vector3(-0.5f, 0.5f, -0.5f), Vector3.UnitY),
        new(new Vector3(0.5f, 0.5f, -0.5f), Vector3.UnitY),
        new(new Vector3(0.5f, 0.5f, 0.5f), Vector3.UnitY),
        new(new Vector3(-0.5f, 0.5f, 0.5f), Vector3.UnitY),

        new(new Vector3(-0.5f, -0.5f, -0.5f), -Vector3.UnitX),
        new(new Vector3(-0.5f, 0.5f, -0.5f), -Vector3.UnitX),
        new(new Vector3(-0.5f, 0.5f, 0.5f), -Vector3.UnitX),
        new(new Vector3(-0.5f, -0.5f, 0.5f), -Vector3.UnitX),

        new(new Vector3(0.5f, -0.5f, -0.5f), Vector3.UnitX),
        new(new Vector3(0.5f, 0.5f, -0.5f), Vector3.UnitX),
        new(new Vector3(0.5f, 0.5f, 0.5f), Vector3.UnitX),
        new(new Vector3(0.5f, -0.5f, 0.5f), Vector3.UnitX),
    ];

    private static readonly ushort[] Indices =
    [
        0, 2, 1, 0, 3, 2,
        4, 5, 6, 4, 6, 7,
        8, 9, 10, 8, 10, 11,
        12, 15, 14, 12, 14, 13,
        16, 18, 17, 16, 19, 18,
        20, 21, 22, 20, 22, 23,
    ];
}
