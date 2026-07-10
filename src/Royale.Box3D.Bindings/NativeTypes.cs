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

[StructLayout(LayoutKind.Sequential)]
public struct B3MotionLocks
{
    [MarshalAs(UnmanagedType.I1)]
    public bool LinearX;
    [MarshalAs(UnmanagedType.I1)]
    public bool LinearY;
    [MarshalAs(UnmanagedType.I1)]
    public bool LinearZ;
    [MarshalAs(UnmanagedType.I1)]
    public bool AngularX;
    [MarshalAs(UnmanagedType.I1)]
    public bool AngularY;
    [MarshalAs(UnmanagedType.I1)]
    public bool AngularZ;
}

public enum B3BodyType
{
    StaticBody = 0,
    KinematicBody = 1,
    DynamicBody = 2,
    BodyTypeCount = 3,
}

[StructLayout(LayoutKind.Sequential)]
public struct B3BodyDef
{
    public B3BodyType Type;
    public B3Pos Position;
    public B3Quat Rotation;
    public B3Vec3 LinearVelocity;
    public B3Vec3 AngularVelocity;
    public float LinearDamping;
    public float AngularDamping;
    public float GravityScale;
    public float SleepThreshold;
    public nint Name;
    public nint UserData;
    public B3MotionLocks MotionLocks;
    [MarshalAs(UnmanagedType.I1)]
    public bool EnableSleep;
    [MarshalAs(UnmanagedType.I1)]
    public bool IsAwake;
    [MarshalAs(UnmanagedType.I1)]
    public bool IsBullet;
    [MarshalAs(UnmanagedType.I1)]
    public bool IsEnabled;
    [MarshalAs(UnmanagedType.I1)]
    public bool AllowFastRotation;
    [MarshalAs(UnmanagedType.I1)]
    public bool EnableContactRecycling;
    public int InternalValue;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3SurfaceMaterial
{
    public float Friction;
    public float Restitution;
    public float RollingResistance;
    public B3Vec3 TangentVelocity;
    public ulong UserMaterialId;
    public uint CustomColor;
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

public enum B3HexColor
{
    DarkGray = 0xA9A9A9,
    Gold = 0xFFD700,
    Green = 0x008000,
    Red = 0xFF0000,
    White = 0xFFFFFF,
    Yellow = 0xFFFF00,
}

[StructLayout(LayoutKind.Sequential)]
public struct B3ShapeDef
{
    public nint Name;
    public nint UserData;
    public nint Materials;
    public int MaterialCount;
    public B3SurfaceMaterial BaseMaterial;
    public float Density;
    public float ExplosionScale;
    public B3Filter Filter;
    [MarshalAs(UnmanagedType.I1)]
    public bool EnableCustomFiltering;
    [MarshalAs(UnmanagedType.I1)]
    public bool IsSensor;
    [MarshalAs(UnmanagedType.I1)]
    public bool EnableSensorEvents;
    [MarshalAs(UnmanagedType.I1)]
    public bool EnableContactEvents;
    [MarshalAs(UnmanagedType.I1)]
    public bool EnableHitEvents;
    [MarshalAs(UnmanagedType.I1)]
    public bool EnablePreSolveEvents;
    [MarshalAs(UnmanagedType.I1)]
    public bool InvokeContactCreation;
    [MarshalAs(UnmanagedType.I1)]
    public bool UpdateBodyMass;
    public int InternalValue;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Capsule
{
    public B3Vec3 Center1;
    public B3Vec3 Center2;
    public float Radius;
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

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public delegate bool B3OverlapResultFcn(B3ShapeId shapeId, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate float B3CastResultFcn(
    B3ShapeId shapeId,
    B3Pos point,
    B3Vec3 normal,
    float fraction,
    ulong userMaterialId,
    int triangleIndex,
    int childIndex,
    nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public delegate bool B3MoverFilterFcn(B3ShapeId shapeId, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public unsafe delegate bool B3PlaneResultFcn(B3ShapeId shapeId, B3PlaneResult* planes, int planeCount, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate nint B3CreateDebugShapeFcn(B3DebugShape* debugShape, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void B3DestroyDebugShapeFcn(nint userShape, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public delegate bool B3DrawShapeFcn(nint userShape, B3WorldTransform transform, B3HexColor color, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void B3DrawSegmentFcn(B3Pos p1, B3Pos p2, B3HexColor color, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void B3DrawTransformFcn(B3WorldTransform transform, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void B3DrawPointFcn(B3Pos p, float size, B3HexColor color, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void B3DrawSphereFcn(B3Pos p, float radius, B3HexColor color, float alpha, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void B3DrawCapsuleFcn(B3Pos p1, B3Pos p2, float radius, B3HexColor color, float alpha, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void B3DrawBoundsFcn(B3Aabb aabb, B3HexColor color, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void B3DrawBoxFcn(B3Vec3 extents, B3WorldTransform transform, B3HexColor color, nint context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void B3DrawStringFcn(B3Pos p, nint text, B3HexColor color, nint context);

[StructLayout(LayoutKind.Sequential)]
public struct B3DebugShape
{
    public B3ShapeId ShapeId;
    public B3ShapeType Type;
    public nint Shape;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3Sphere
{
    public B3Vec3 Center;
    public float Radius;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3DebugDraw
{
    public nint DrawShapeFcn;
    public nint DrawSegmentFcn;
    public nint DrawTransformFcn;
    public nint DrawPointFcn;
    public nint DrawSphereFcn;
    public nint DrawCapsuleFcn;
    public nint DrawBoundsFcn;
    public nint DrawBoxFcn;
    public nint DrawStringFcn;
    public B3Aabb DrawingBounds;
    public float ForceScale;
    public float JointScale;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawShapes;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawJoints;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawJointExtras;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawBounds;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawMass;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawSleep;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawBodyNames;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawContacts;
    public int DrawAnchorA;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawGraphColors;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawContactFeatures;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawContactNormals;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawContactForces;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawFrictionForces;
    [MarshalAs(UnmanagedType.I1)]
    public bool DrawIslands;
    public nint Context;
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

[StructLayout(LayoutKind.Sequential)]
public struct B3HullVertex
{
    public byte Edge;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3HullHalfEdge
{
    public byte Next;
    public byte Twin;
    public byte Origin;
    public byte Face;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3HullFace
{
    public byte Edge;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3HullData
{
    public ulong Version;
    public int ByteCount;
    public uint Hash;
    public B3Aabb Aabb;
    public float SurfaceArea;
    public float Volume;
    public float InnerRadius;
    public B3Vec3 Center;
    public B3Matrix3 CentralInertia;
    public int VertexCount;
    public int VertexOffset;
    public int PointOffset;
    public int EdgeCount;
    public int EdgeOffset;
    public int FaceCount;
    public int FaceOffset;
    public int PlaneOffset;
    public int Padding;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct B3BoxHull
{
    public B3HullData Base;
    public fixed byte BoxVertices[8];
    public fixed float BoxPoints[24];
    public fixed byte BoxEdges[96];
    public fixed byte BoxFaces[6];
    public fixed byte Padding[2];
    public fixed float BoxPlanes[24];
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct B3MeshDef
{
    public B3Vec3* Vertices;
    public int* Indices;
    public byte* MaterialIndices;
    public float WeldTolerance;
    public int VertexCount;
    public int TriangleCount;
    [MarshalAs(UnmanagedType.I1)]
    public bool WeldVertices;
    [MarshalAs(UnmanagedType.I1)]
    public bool UseMedianSplit;
    [MarshalAs(UnmanagedType.I1)]
    public bool IdentifyEdges;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3MeshTriangle
{
    public int Index1;
    public int Index2;
    public int Index3;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3MeshNode
{
    public B3Vec3 LowerBound;
    public uint Data;
    public B3Vec3 UpperBound;
    public uint TriangleOffset;
}

[StructLayout(LayoutKind.Sequential)]
public struct B3MeshData
{
    public ulong Version;
    public int ByteCount;
    public uint Hash;
    public B3Aabb Bounds;
    public float SurfaceArea;
    public int TreeHeight;
    public int DegenerateCount;
    public int NodeOffset;
    public int NodeCount;
    public int VertexOffset;
    public int VertexCount;
    public int TriangleOffset;
    public int TriangleCount;
    public int MaterialOffset;
    public int MaterialCount;
    public int FlagsOffset;
}
