using Microsoft.Win32.SafeHandles;
using Royale.Box3D.Bindings.Interop;

namespace Royale.Box3D.Geometry;

public sealed unsafe class Box3DHull : IDisposable
{
    private readonly Box3DHullSafeHandle handle;
    private int disposed;

    private Box3DHull(Box3DHullSafeHandle handle, int vertexCount)
    {
        this.handle = handle;
        VertexCount = vertexCount;
    }

    public int VertexCount { get; }

    public bool IsDisposed => Volatile.Read(ref disposed) != 0;

    public bool IsNativeReleased => handle.IsReleased;

    public static Box3DHull Create(ReadOnlySpan<B3Vec3> points, int? maxVertexCount = null)
    {
        if (points.Length < 4)
            throw new ArgumentException("A Box3D hull requires at least four points.", nameof(points));
        ValidateFinite(points, nameof(points));

        int maximum = maxVertexCount ?? points.Length;
        if (maximum < 4)
            throw new ArgumentOutOfRangeException(nameof(maxVertexCount), "Maximum vertex count must be at least four.");

        fixed (B3Vec3* pointPointer = points)
        {
            B3HullData* nativeHull = Box3DBindingSurface.b3CreateHull(pointPointer, points.Length, maximum);
            if (nativeHull is null)
                throw new InvalidDataException("Box3D rejected the supplied convex hull points.");
            return new Box3DHull(new Box3DHullSafeHandle(nativeHull), nativeHull->VertexCount);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            handle.Dispose();
    }

    internal B3HullData* GetNativeData()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return (B3HullData*)handle.DangerousGetHandle();
    }

    private static void ValidateFinite(ReadOnlySpan<B3Vec3> points, string parameterName)
    {
        foreach (B3Vec3 point in points)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
                throw new ArgumentException("Hull points must be finite.", parameterName);
        }
    }

    private sealed class Box3DHullSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private int released;

        public Box3DHullSafeHandle(B3HullData* hull)
            : base(ownsHandle: true)
        {
            SetHandle((nint)hull);
        }

        protected override bool ReleaseHandle()
        {
            Box3DBindingSurface.b3DestroyHull((B3HullData*)handle);
            Interlocked.Exchange(ref released, 1);
            return true;
        }

        public bool IsReleased => Volatile.Read(ref released) != 0;
    }
}
