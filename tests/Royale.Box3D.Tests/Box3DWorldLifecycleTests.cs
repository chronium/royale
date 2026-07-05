using Royale.Box3D.Bindings;

namespace Royale.Box3D.Tests;

public sealed class Box3DWorldLifecycleTests
{
    [Fact]
    public void DefaultWorldDefinitionReturnsUsableDefaults()
    {
        B3WorldDef worldDef = Box3DBindingSurface.b3DefaultWorldDef();

        Assert.Equal(0.0f, worldDef.Gravity.X);
        Assert.Equal(-10.0f, worldDef.Gravity.Y);
        Assert.Equal(0.0f, worldDef.Gravity.Z);
        Assert.Equal(1.0f, worldDef.RestitutionThreshold);
        Assert.Equal(1.0f, worldDef.HitEventThreshold);
        Assert.Equal(30.0f, worldDef.ContactHertz);
        Assert.Equal(10.0f, worldDef.ContactDampingRatio);
        Assert.Equal(3.0f, worldDef.ContactSpeed);
        Assert.Equal(400.0f, worldDef.MaximumLinearSpeed);
        Assert.True(worldDef.EnableSleep);
        Assert.True(worldDef.EnableContinuous);
        Assert.Equal(0u, worldDef.WorkerCount);
        Assert.Equal(0, worldDef.FrictionCallback);
        Assert.Equal(0, worldDef.RestitutionCallback);
        Assert.Equal(0, worldDef.EnqueueTask);
        Assert.Equal(0, worldDef.FinishTask);
        Assert.Equal(0, worldDef.UserTaskContext);
        Assert.Equal(0, worldDef.UserData);
        Assert.Equal(0, worldDef.CreateDebugShape);
        Assert.Equal(0, worldDef.DestroyDebugShape);
        Assert.Equal(0, worldDef.UserDebugShapeContext);
        Assert.Equal(0, worldDef.Capacity.StaticShapeCount);
        Assert.Equal(0, worldDef.Capacity.DynamicShapeCount);
        Assert.Equal(0, worldDef.Capacity.StaticBodyCount);
        Assert.Equal(0, worldDef.Capacity.DynamicBodyCount);
        Assert.Equal(0, worldDef.Capacity.ContactCount);
        Assert.Equal(1152023, worldDef.InternalValue);
    }

    [Fact]
    public void CreateStepAndDestroyWorldPreservesNativeLifecycle()
    {
        int initialWorldCount = Box3DBindingSurface.b3GetWorldCount();
        int initialMaxWorldCount = Box3DBindingSurface.b3GetMaxWorldCount();
        B3WorldDef worldDef = Box3DBindingSurface.b3DefaultWorldDef();

        B3WorldId worldId = Box3DBindingSurface.b3CreateWorld(in worldDef);
        try
        {
            Assert.NotEqual(0, worldId.Index1);
            Assert.True(Box3DBindingSurface.b3World_IsValid(worldId));
            Assert.Equal(initialWorldCount + 1, Box3DBindingSurface.b3GetWorldCount());
            Assert.True(Box3DBindingSurface.b3GetMaxWorldCount() >= initialMaxWorldCount);

            for (int i = 0; i < 4; i++)
            {
                Box3DBindingSurface.b3World_Step(worldId, 1.0f / 60.0f, 4);
            }

            Assert.True(Box3DBindingSurface.b3World_IsValid(worldId));
        }
        finally
        {
            if (Box3DBindingSurface.b3World_IsValid(worldId))
            {
                Box3DBindingSurface.b3DestroyWorld(worldId);
            }
        }

        Assert.False(Box3DBindingSurface.b3World_IsValid(worldId));
        Assert.Equal(initialWorldCount, Box3DBindingSurface.b3GetWorldCount());
    }
}
