using Royale.Box3D.Bindings.Interop;

using Royale.Box3D.Tests.Infrastructure;

using Royale.Box3D.Bodies;
using Royale.Box3D.Geometry;
using Royale.Box3D.Runtime;
using Royale.Box3D.Worlds;

namespace Royale.Box3D.Tests.Runtime;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class Box3DHelloWorldTests
{
    [Fact]
    public void FallingBoxSettlesOnGroundLikeUpstreamHelloWorld()
    {
        B3WorldDef worldDef = Box3DBindingSurface.b3DefaultWorldDef();
        worldDef.Gravity = new B3Vec3 { X = 0.0f, Y = -10.0f, Z = 0.0f };

        B3WorldId worldId = Box3DBindingSurface.b3CreateWorld(in worldDef);
        try
        {
            B3BodyDef groundBodyDef = Box3DBindingSurface.b3DefaultBodyDef();
            groundBodyDef.Position = new B3Pos { X = 0.0f, Y = -10.0f, Z = 0.0f };

            B3BodyId groundId = Box3DBindingSurface.b3CreateBody(worldId, in groundBodyDef);
            Assert.True(Box3DBindingSurface.b3Body_IsValid(groundId));

            B3BoxHull groundBox = Box3DBindingSurface.b3MakeBoxHull(50.0f, 10.0f, 50.0f);
            B3ShapeDef groundShapeDef = Box3DBindingSurface.b3DefaultShapeDef();
            B3ShapeId groundShapeId = Box3DBindingSurface.b3CreateHullShape(groundId, in groundShapeDef, in groundBox.Base);
            Assert.NotEqual(0, groundShapeId.Index1);

            B3BodyDef bodyDef = Box3DBindingSurface.b3DefaultBodyDef();
            bodyDef.Type = B3BodyType.DynamicBody;
            bodyDef.Position = new B3Pos { X = 0.0f, Y = 4.0f, Z = 0.0f };

            B3BodyId bodyId = Box3DBindingSurface.b3CreateBody(worldId, in bodyDef);
            Assert.True(Box3DBindingSurface.b3Body_IsValid(bodyId));

            B3BoxHull dynamicBox = Box3DBindingSurface.b3MakeCubeHull(1.0f);
            B3ShapeDef shapeDef = Box3DBindingSurface.b3DefaultShapeDef();
            shapeDef.Density = 1.0f;
            shapeDef.BaseMaterial.Friction = 0.3f;

            B3ShapeId dynamicShapeId = Box3DBindingSurface.b3CreateHullShape(bodyId, in shapeDef, in dynamicBox.Base);
            Assert.NotEqual(0, dynamicShapeId.Index1);

            B3Pos initialPosition = Box3DBindingSurface.b3Body_GetPosition(bodyId);
            Assert.InRange(initialPosition.Y, 3.99f, 4.01f);

            for (int i = 0; i < 90; i++)
            {
                Box3DBindingSurface.b3World_Step(worldId, 1.0f / 60.0f, 4);
            }

            B3Pos finalPosition = Box3DBindingSurface.b3Body_GetPosition(bodyId);
            B3Quat finalRotation = Box3DBindingSurface.b3Body_GetRotation(bodyId);

            Assert.True(finalPosition.Y < initialPosition.Y);
            Assert.InRange(finalPosition.Y, 0.98f, 1.05f);
            Assert.InRange(finalPosition.X, -0.05f, 0.05f);
            Assert.InRange(finalPosition.Z, -0.05f, 0.05f);
            Assert.InRange(finalRotation.V.X, -0.001f, 0.001f);
            Assert.InRange(finalRotation.V.Y, -0.001f, 0.001f);
            Assert.InRange(finalRotation.V.Z, -0.001f, 0.001f);
            Assert.InRange(finalRotation.S, 0.999f, 1.001f);
        }
        finally
        {
            if (Box3DBindingSurface.b3World_IsValid(worldId))
            {
                Box3DBindingSurface.b3DestroyWorld(worldId);
            }
        }
    }
}
