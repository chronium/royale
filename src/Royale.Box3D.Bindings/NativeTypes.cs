using System.Runtime.InteropServices;

namespace Royale.Box3D.Bindings;

[StructLayout(LayoutKind.Sequential)]
public struct B3Vec2
{
    public float X;
    public float Y;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Vec3
{
    public float X;
    public float Y;
    public float Z;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3CosSin
{
    public float Cosine;
    public float Sine;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Quat
{
    public B3Vec3 V;
    public float S;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Transform
{
    public B3Vec3 P;
    public B3Quat Q;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Pos
{
    public float X;
    public float Y;
    public float Z;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3WorldTransform
{
    public B3Pos P;
    public B3Quat Q;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Matrix3
{
    public B3Vec3 Cx;
    public B3Vec3 Cy;
    public B3Vec3 Cz;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Aabb
{
    public B3Vec3 LowerBound;
    public B3Vec3 UpperBound;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Plane
{
    public B3Vec3 Normal;
    public float Offset;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3WorldId
{
    public ushort Index1;
    public ushort Generation;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3BodyId
{
    public int Index1;
    public ushort World0;
    public ushort Generation;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3ShapeId
{
    public int Index1;
    public ushort World0;
    public ushort Generation;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3JointId
{
    public int Index1;
    public ushort World0;
    public ushort Generation;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3ContactId
{
    public int Index1;
    public ushort World0;
    public short Padding;
    public uint Generation;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Capacity
{
    public int StaticShapeCount;
    public int DynamicShapeCount;
    public int StaticBodyCount;
    public int DynamicBodyCount;
    public int ContactCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3WorldDef
{
    public B3Vec3 Gravity;
    public float RestitutionThreshold;
    public float HitEventThreshold;
    public float ContactHertz;
    public float ContactDampingRatio;
    public float ContactSpeed;
    public float MaximumLinearSpeed;
    public nint FrictionCallback;
    public nint RestitutionCallback;
    [MarshalAs(UnmanagedType.I1)]
    public bool EnableSleep;
    [MarshalAs(UnmanagedType.I1)]
    public bool EnableContinuous;
    public uint WorkerCount;
    public nint EnqueueTask;
    public nint FinishTask;
    public nint UserTaskContext;
    public nint UserData;
    public nint CreateDebugShape;
    public nint DestroyDebugShape;
    public nint UserDebugShapeContext;
    public B3Capacity Capacity;
    public int InternalValue;
}

public enum B3BodyType
{
    StaticBody = 0,
    KinematicBody = 1,
    DynamicBody = 2,
    BodyTypeCount = 3,
}

public enum B3ShapeType
{
    CapsuleShape = 0,
    CompoundShape = 1,
    HeightShape = 2,
    HullShape = 3,
    MeshShape = 4,
    SphereShape = 5,
    ShapeTypeCount = 6,
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Version
{
    public int Major;
    public int Minor;
    public int Revision;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Filter
{
    public ulong CategoryBits;
    public ulong MaskBits;
    public int GroupIndex;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3QueryFilter
{
    public ulong CategoryBits;
    public ulong MaskBits;
    public ulong Id;
    public nint Name;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Profile
{
    public float Step;
    public float Pairs;
    public float Collide;
    public float Solve;
    public float SolverSetup;
    public float Constraints;
    public float PrepareConstraints;
    public float IntegrateVelocities;
    public float WarmStart;
    public float SolveImpulses;
    public float IntegratePositions;
    public float RelaxImpulses;
    public float ApplyRestitution;
    public float StoreImpulses;
    public float SplitIslands;
    public float Transforms;
    public float SensorHits;
    public float JointEvents;
    public float HitEvents;
    public float Refit;
    public float Bullets;
    public float SleepIslands;
    public float Sensors;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct B3Counters
{
    public int BodyCount;
    public int ShapeCount;
    public int ContactCount;
    public int JointCount;
    public int IslandCount;
    public int StackUsed;
    public int ArenaCapacity;
    public int StaticTreeHeight;
    public int TreeHeight;
    public int SatCallCount;
    public int SatCacheHitCount;
    public int ByteCount;
    public int TaskCount;
    public fixed int ColorCounts[24];
    public fixed int ManifoldCounts[8];
    public int AwakeContactCount;
    public int RecycledContactCount;
    public int DistanceIterations;
    public int PushBackIterations;
    public int RootIterations;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3RayCastInput
{
    public B3Vec3 Origin;
    public B3Vec3 Translation;
    public float MaxFraction;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3RayResult
{
    public B3ShapeId ShapeId;
    public B3Pos Point;
    public B3Vec3 Normal;
    public ulong UserMaterialId;
    public float Fraction;
    public int TriangleIndex;
    public int ChildIndex;
    public int NodeVisits;
    public int LeafVisits;
    [MarshalAs(UnmanagedType.I1)]
    public bool Hit;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3ShapeProxy
{
    public nint Points;
    public int Count;
    public float Radius;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3ShapeCastInput
{
    public B3ShapeProxy Proxy;
    public B3Vec3 Translation;
    public float MaxFraction;
    [MarshalAs(UnmanagedType.I1)]
    public bool CanEncroach;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3BoxCastInput
{
    public B3Aabb Box;
    public B3Vec3 Translation;
    public float MaxFraction;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3CastOutput
{
    public B3Vec3 Normal;
    public B3Vec3 Point;
    public float Fraction;
    public int Iterations;
    public int TriangleIndex;
    public int ChildIndex;
    public int MaterialIndex;
    [MarshalAs(UnmanagedType.I1)]
    public bool Hit;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3WorldCastOutput
{
    public B3Vec3 Normal;
    public B3Pos Point;
    public float Fraction;
    public int Iterations;
    public int TriangleIndex;
    public int ChildIndex;
    public int MaterialIndex;
    [MarshalAs(UnmanagedType.I1)]
    public bool Hit;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3BodyCastResult
{
    public B3ShapeId ShapeId;
    public B3Pos Point;
    public B3Vec3 Normal;
    public float Fraction;
    public int TriangleIndex;
    public ulong UserMaterialId;
    public int Iterations;
    [MarshalAs(UnmanagedType.I1)]
    public bool Hit;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3TreeStats
{
    public int NodeVisits;
    public int LeafVisits;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3PlaneResult
{
    public B3Plane Plane;
    public B3Vec3 Point;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3CollisionPlane
{
    public B3Plane Plane;
    public float PushLimit;
    public float Push;
    [MarshalAs(UnmanagedType.I1)]
    public bool ClipVelocity;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3PlaneSolverResult
{
    public B3Vec3 Delta;
    public int IterationCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3BodyPlaneResult
{
    public B3ShapeId ShapeId;
    public B3PlaneResult Result;
}
