using System.Numerics;

namespace Royale.Client.Rendering;

public static class UnitBoxMesh
{
    public static StaticMeshGeometry Create()
    {
        return new StaticMeshGeometry(Vertices, Indices);
    }

    private static readonly StaticMeshVertex[] Vertices =
    [
        new(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.90f, 0.16f, 0.15f)),
        new(new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.90f, 0.16f, 0.15f)),
        new(new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.90f, 0.16f, 0.15f)),
        new(new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.90f, 0.16f, 0.15f)),

        new(new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.10f, 0.55f, 0.95f)),
        new(new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.10f, 0.55f, 0.95f)),
        new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.10f, 0.55f, 0.95f)),
        new(new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.10f, 0.55f, 0.95f)),

        new(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.95f, 0.76f, 0.16f)),
        new(new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.95f, 0.76f, 0.16f)),
        new(new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.95f, 0.76f, 0.16f)),
        new(new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.95f, 0.76f, 0.16f)),

        new(new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.20f, 0.76f, 0.34f)),
        new(new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.20f, 0.76f, 0.34f)),
        new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.20f, 0.76f, 0.34f)),
        new(new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.20f, 0.76f, 0.34f)),

        new(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.70f, 0.32f, 0.92f)),
        new(new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.70f, 0.32f, 0.92f)),
        new(new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.70f, 0.32f, 0.92f)),
        new(new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.70f, 0.32f, 0.92f)),

        new(new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.95f, 0.44f, 0.16f)),
        new(new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.95f, 0.44f, 0.16f)),
        new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.95f, 0.44f, 0.16f)),
        new(new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.95f, 0.44f, 0.16f)),
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
