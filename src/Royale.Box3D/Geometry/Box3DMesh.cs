using Microsoft.Win32.SafeHandles;
using Royale.Box3D.Bindings.Interop;

namespace Royale.Box3D.Geometry;

public readonly record struct Box3DMeshCreationSettings(
    float WeldTolerance,
    bool WeldVertices,
    bool UseMedianSplit,
    bool IdentifyEdges);

public sealed unsafe class Box3DMesh : IDisposable
{
    private readonly Box3DMeshSafeHandle handle;
    private int disposed;

    private Box3DMesh(Box3DMeshSafeHandle handle, int vertexCount, int triangleCount, int treeHeight)
    {
        this.handle = handle;
        VertexCount = vertexCount;
        TriangleCount = triangleCount;
        TreeHeight = treeHeight;
    }

    public int VertexCount { get; }

    public int TriangleCount { get; }

    public int TreeHeight { get; }

    public bool IsDisposed => Volatile.Read(ref disposed) != 0;

    public bool IsNativeReleased => handle.IsReleased;

    public static Box3DMesh Create(
        ReadOnlySpan<B3Vec3> vertices,
        ReadOnlySpan<int> indices,
        in Box3DMeshCreationSettings settings)
    {
        Validate(vertices, indices, in settings);

        int triangleCount = indices.Length / 3;
        int[] degenerateTriangles = new int[triangleCount];
        fixed (B3Vec3* vertexPointer = vertices)
        fixed (int* indexPointer = indices)
        fixed (int* degeneratePointer = degenerateTriangles)
        {
            var definition = new B3MeshDef
            {
                Vertices = vertexPointer,
                Indices = indexPointer,
                MaterialIndices = null,
                WeldTolerance = settings.WeldTolerance,
                VertexCount = vertices.Length,
                TriangleCount = triangleCount,
                WeldVertices = settings.WeldVertices,
                UseMedianSplit = settings.UseMedianSplit,
                IdentifyEdges = settings.IdentifyEdges,
            };
            B3MeshData* nativeMesh = Box3DBindingSurface.b3CreateMesh(
                in definition,
                degeneratePointer,
                degenerateTriangles.Length);
            if (nativeMesh is null)
                throw new InvalidDataException("Box3D rejected the supplied static mesh geometry.");
            if (nativeMesh->DegenerateCount != 0)
            {
                int count = nativeMesh->DegenerateCount;
                Box3DBindingSurface.b3DestroyMesh(nativeMesh);
                throw new InvalidDataException($"Box3D rejected {count} degenerate static mesh triangle(s).");
            }

            return new Box3DMesh(
                new Box3DMeshSafeHandle(nativeMesh),
                nativeMesh->VertexCount,
                nativeMesh->TriangleCount,
                nativeMesh->TreeHeight);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            handle.Dispose();
    }

    internal Box3DMeshReference AcquireShapeReference()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return new Box3DMeshReference(handle);
    }

    private static void Validate(
        ReadOnlySpan<B3Vec3> vertices,
        ReadOnlySpan<int> indices,
        in Box3DMeshCreationSettings settings)
    {
        if (vertices.Length < 3)
            throw new ArgumentException("A Box3D mesh requires at least three vertices.", nameof(vertices));
        if (indices.Length == 0 || indices.Length % 3 != 0)
            throw new ArgumentException("Mesh indices must contain complete triangles.", nameof(indices));
        if (!float.IsFinite(settings.WeldTolerance) || settings.WeldTolerance < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Mesh weld tolerance must be finite and non-negative.");

        foreach (B3Vec3 vertex in vertices)
        {
            if (!float.IsFinite(vertex.X) || !float.IsFinite(vertex.Y) || !float.IsFinite(vertex.Z))
                throw new ArgumentException("Mesh vertices must be finite.", nameof(vertices));
        }

        for (int offset = 0; offset < indices.Length; offset += 3)
        {
            int indexA = indices[offset];
            int indexB = indices[offset + 1];
            int indexC = indices[offset + 2];
            if ((uint)indexA >= vertices.Length || (uint)indexB >= vertices.Length || (uint)indexC >= vertices.Length)
                throw new ArgumentException("Mesh indices must reference the supplied vertex array.", nameof(indices));
            if (indexA == indexB || indexA == indexC || indexB == indexC)
                throw new InvalidDataException($"Mesh triangle {offset / 3} repeats a vertex index.");

            B3Vec3 a = vertices[indexA];
            B3Vec3 b = vertices[indexB];
            B3Vec3 c = vertices[indexC];
            float crossX = (b.Y - a.Y) * (c.Z - a.Z) - (b.Z - a.Z) * (c.Y - a.Y);
            float crossY = (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z);
            float crossZ = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (crossX * crossX + crossY * crossY + crossZ * crossZ <= 1e-12f)
                throw new InvalidDataException($"Mesh triangle {offset / 3} is degenerate.");
        }
    }

    internal sealed class Box3DMeshReference : IDisposable
    {
        private Box3DMeshSafeHandle? handle;

        internal Box3DMeshReference(Box3DMeshSafeHandle handle)
        {
            this.handle = handle;
            bool success = false;
            handle.DangerousAddRef(ref success);
            if (!success)
                throw new ObjectDisposedException(nameof(Box3DMesh));
        }

        public B3MeshData* NativeData =>
            (B3MeshData*)(handle ?? throw new ObjectDisposedException(nameof(Box3DMeshReference))).DangerousGetHandle();

        public void Dispose()
        {
            Box3DMeshSafeHandle? current = Interlocked.Exchange(ref handle, null);
            current?.DangerousRelease();
        }
    }

    internal sealed class Box3DMeshSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private int released;

        public Box3DMeshSafeHandle(B3MeshData* mesh)
            : base(ownsHandle: true)
        {
            SetHandle((nint)mesh);
        }

        protected override bool ReleaseHandle()
        {
            Box3DBindingSurface.b3DestroyMesh((B3MeshData*)handle);
            Interlocked.Exchange(ref released, 1);
            return true;
        }

        public bool IsReleased => Volatile.Read(ref released) != 0;
    }
}
