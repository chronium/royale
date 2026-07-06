using System.Numerics;
using Royale.Content;
using Royale.Simulation;

namespace Royale.Simulation.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class KinematicCharacterControllerTests
{
    private const float Tick = 1.0f / 60.0f;

    [Fact]
    public void FallingUnderGravityLandsOnGrayboxFloorAndBecomesGrounded()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(new Vector3(3.0f, 2.0f, 3.0f), Vector3.Zero, false);

        state = StepMany(controller, collisionWorld, state, new KinematicCharacterInput(Vector2.Zero, false), 120).State;

        Assert.True(state.IsGrounded);
        Assert.InRange(state.Position.Y, -0.001f, 0.02f);
        Assert.Equal(0.0f, state.Velocity.Y, precision: 4);
    }

    [Fact]
    public void JumpIsAcceptedOnlyWhileGrounded()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateFloorMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState grounded = StepMany(
            controller,
            collisionWorld,
            new KinematicCharacterState(new Vector3(0.0f, 0.05f, 0.0f), Vector3.Zero, false),
            new KinematicCharacterInput(Vector2.Zero, false),
            10).State;

        KinematicCharacterStepResult groundedJump = controller.Step(
            collisionWorld,
            grounded,
            new KinematicCharacterInput(Vector2.Zero, true),
            Tick);

        KinematicCharacterStepResult airborneJump = controller.Step(
            collisionWorld,
            new KinematicCharacterState(new Vector3(0.0f, 2.0f, 0.0f), Vector3.Zero, false),
            new KinematicCharacterInput(Vector2.Zero, true),
            Tick);

        Assert.True(grounded.IsGrounded);
        Assert.True(groundedJump.JumpAccepted);
        Assert.True(groundedJump.State.Velocity.Y > 0.0f);
        Assert.False(airborneJump.JumpAccepted);
        Assert.True(airborneJump.State.Velocity.Y < 0.0f);
    }

    [Fact]
    public void HorizontalMovementCrossesClearGroundAtWalkSpeed()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateFloorMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        state = StepMany(controller, collisionWorld, state, new KinematicCharacterInput(new Vector2(1.0f, 0.0f), false), 60).State;

        Assert.True(state.IsGrounded);
        Assert.InRange(state.Position.X, 4.40f, 4.60f);
        Assert.InRange(MathF.Abs(state.Position.Z), 0.0f, 0.001f);
    }

    [Fact]
    public void WallCollisionPreventsPassingThroughStaticGeometry()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateWallMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        state = StepMany(controller, collisionWorld, state, new KinematicCharacterInput(new Vector2(1.0f, 0.0f), false), 80).State;

        Assert.True(state.IsGrounded);
        Assert.InRange(state.Position.X, 1.45f, 1.56f);
    }

    [Fact]
    public void WallSlidingPreservesTangentialMovement()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateWallMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        state = StepMany(controller, collisionWorld, state, new KinematicCharacterInput(Vector2.One, false), 80).State;

        Assert.True(state.IsGrounded);
        Assert.InRange(state.Position.X, 1.45f, 1.56f);
        Assert.True(state.Position.Z > 2.0f);
    }

    [Fact]
    public void SteepSlopeIsRejectedAsGround()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateSteepSlopeMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(new Vector3(0.0f, 0.08f, -0.1f), Vector3.Zero, false);

        KinematicCharacterStepResult result = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(Vector2.Zero, false),
            Tick);

        Assert.False(result.State.IsGrounded);
        Assert.True(result.State.Velocity.Y < 0.0f);
    }

    [Fact]
    public void GroundedMovementStepsUpOntoLowObstacle()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateLowStepMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);
        KinematicCharacterStepResult result = default;
        KinematicCharacterStepResult stepResult = default;

        for (int i = 0; i < 24; i++)
        {
            result = controller.Step(
                collisionWorld,
                state,
                new KinematicCharacterInput(new Vector2(0.0f, 1.0f), false),
                Tick);
            state = result.State;
            if (result.Stepped && stepResult == default)
                stepResult = result;
        }

        Assert.True(stepResult.Stepped);
        Assert.InRange(stepResult.State.Position.Y, 0.08f, 0.14f);
        Assert.True(result.State.IsGrounded);
        Assert.True(result.State.Position.Z > 1.0f);
    }

    [Fact]
    public void CeilingCollisionStopsUpwardMotion()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateCeilingMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);
        state = controller.Step(collisionWorld, state, new KinematicCharacterInput(Vector2.Zero, true), Tick).State;

        KinematicCharacterStepResult result = default;
        for (int i = 0; i < 80; i++)
        {
            result = controller.Step(collisionWorld, state, new KinematicCharacterInput(Vector2.Zero, false), Tick);
            state = result.State;
            if (result.HitCeiling)
                break;
        }

        Assert.True(result.HitCeiling);
        Assert.Equal(0.0f, result.State.Velocity.Y, precision: 4);
        Assert.True(result.State.Position.Y < 0.7f);
    }

    [Fact]
    public void PenetrationRecoveryNudgesCapsuleOutOfSmallOverlap()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateWallMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(new Vector3(1.60f, 0.0f, 0.0f), Vector3.Zero, true);

        KinematicCharacterStepResult result = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(Vector2.Zero, false),
            Tick);

        Assert.True(result.State.Position.X < state.Position.X);
        Assert.InRange(result.State.Position.X, 1.50f, 1.56f);
    }

    private static KinematicCharacterStepResult StepMany(
        KinematicCharacterController controller,
        MapStaticCollisionWorld collisionWorld,
        KinematicCharacterState state,
        KinematicCharacterInput input,
        int ticks)
    {
        KinematicCharacterStepResult result = default;
        for (int i = 0; i < ticks; i++)
        {
            result = controller.Step(collisionWorld, state, input, Tick);
            state = result.State;
        }

        return result;
    }

    private static GameMap CreateFloorMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)));

    private static GameMap CreateWallMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)),
        Box("wall", new Vector3(2.0f, 1.0f, 0.0f), new Vector3(0.2f, 2.0f, 8.0f)));

    private static GameMap CreateLowStepMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)),
        Box("low-step", new Vector3(0.0f, 0.05f, 1.0f), new Vector3(3.0f, 0.1f, 0.35f)));

    private static GameMap CreateCeilingMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)),
        Box("ceiling", new Vector3(0.0f, 2.5f, 0.0f), new Vector3(4.0f, 0.2f, 4.0f)));

    private static GameMap CreateSteepSlopeMap() => CreateMap(
        Box(
            "steep-slope",
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(4.0f, 0.2f, 4.0f),
            new Vector3(-60.0f, 0.0f, 0.0f)));

    private static GameMap CreateMap(params StaticBoxDefinition[] boxes) => new()
    {
        Id = "test-map",
        Name = "Test Map",
        StaticBoxes = boxes.ToList(),
    };

    private static StaticBoxDefinition Box(string id, Vector3 position, Vector3 size, Vector3? rotationEuler = null) => new()
    {
        Id = id,
        Position = new MapVector3(position.X, position.Y, position.Z),
        Size = new MapVector3(size.X, size.Y, size.Z),
        RotationEuler = rotationEuler is { } rotation
            ? new MapVector3(rotation.X, rotation.Y, rotation.Z)
            : new MapVector3(),
    };
}
