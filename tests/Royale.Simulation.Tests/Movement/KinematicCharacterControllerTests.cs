using System.Numerics;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

using Royale.Simulation.Tests.Infrastructure;

namespace Royale.Simulation.Tests.Movement;

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
    public void ShallowAngleWallSlideDoesNotOscillatePerpendicularToWall()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateWallMap());
        var controller = new KinematicCharacterController();

        foreach ((float x, float z) in new[]
        {
            (0.1763f, 1.0f),
            (0.2679f, 1.0f),
            (0.3640f, 1.0f),
        })
        {
            KinematicCharacterState state = new(new Vector3(1.52f, 0.0f, -2.5f), Vector3.Zero, true);
            Vector2 move = Vector2.Normalize(new Vector2(x, z));
            var slidePositions = new List<Vector3>();

            for (int i = 0; i < 80; i++)
            {
                state = controller.Step(
                    collisionWorld,
                    state,
                    new KinematicCharacterInput(move, false),
                    Tick).State;

                if (i >= 20 && i <= 45)
                    slidePositions.Add(state.Position);
            }

            float minX = slidePositions.Min(position => position.X);
            float maxX = slidePositions.Max(position => position.X);
            Assert.InRange(maxX - minX, 0.0f, 0.002f);
            Assert.All(slidePositions, position => Assert.InRange(position.Y, -0.001f, 0.02f));
        }
    }

    [Fact]
    public void HighSpeedMovementIntoThinWallIsBlocked()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateThinWallMap());
        var controller = new KinematicCharacterController(new KinematicCharacterSettings { WalkSpeed = 80.0f });
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        state = StepMany(controller, collisionWorld, state, new KinematicCharacterInput(new Vector2(1.0f, 0.0f), false), 6).State;

        Assert.True(state.IsGrounded);
        AssertFinite(state);
        Assert.InRange(state.Position.X, 1.45f, 1.66f);
    }

    [Fact]
    public void DiagonalMovementIntoCornerCannotEscapeThroughPerpendicularWalls()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateCornerMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        state = StepMany(controller, collisionWorld, state, new KinematicCharacterInput(Vector2.One, false), 120).State;

        Assert.True(state.IsGrounded);
        AssertFinite(state);
        Assert.InRange(state.Position.X, 1.45f, 1.56f);
        Assert.InRange(state.Position.Z, 1.45f, 1.56f);
    }

    [Fact]
    public void RepeatedMovementIntoCornerRemainsFiniteAndStable()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateCornerMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        state = StepMany(controller, collisionWorld, state, new KinematicCharacterInput(Vector2.One, false), 600).State;

        Assert.True(state.IsGrounded);
        AssertFinite(state);
        Assert.InRange(state.Position.X, 1.45f, 1.56f);
        Assert.InRange(state.Position.Z, 1.45f, 1.56f);
        Assert.InRange(state.Velocity.Y, -0.001f, 0.001f);
    }

    [Fact]
    public void WalkableSlopeCountsAsGround()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateWalkableSlopeMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(new Vector3(0.0f, 1.0f, 0.0f), Vector3.Zero, false);

        state = StepMany(controller, collisionWorld, state, new KinematicCharacterInput(Vector2.Zero, false), 90).State;

        Assert.True(state.IsGrounded);
        AssertFinite(state);
        Assert.Equal(0.0f, state.Velocity.Y, precision: 4);
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
    public void GroundedMovementIsBlockedByObstacleAboveMaxStepHeight()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateHighStepMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        KinematicCharacterStepResult result = StepMany(
            controller,
            collisionWorld,
            state,
            new KinematicCharacterInput(new Vector2(0.0f, 1.0f), false),
            60);

        Assert.False(result.Stepped);
        Assert.True(result.State.IsGrounded);
        Assert.InRange(result.State.Position.Y, -0.001f, 0.02f);
        Assert.InRange(result.State.Position.Z, 0.45f, 0.55f);
    }

    [Fact]
    public void SlidingPastTooTallStepEdgeDoesNotAcceptFloorHeightShortcutStep()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateGrayboxStepLowMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(new Vector3(-3.6f, 0.0f, -0.4f), Vector3.Zero, true);
        Vector2 move = Vector2.Normalize(new Vector2(1.0f, 1.0f));

        for (int i = 0; i < 45; i++)
        {
            KinematicCharacterStepResult result = controller.Step(
                collisionWorld,
                state,
                new KinematicCharacterInput(move, false),
                Tick);

            Assert.False(result.Stepped);
            Assert.True(result.Displacement.Z >= -0.001f);
            state = result.State;
        }
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
    public void JumpingIntoLowCeilingDoesNotPenetrateOrPopThrough()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateCeilingMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        state = controller.Step(collisionWorld, state, new KinematicCharacterInput(Vector2.Zero, true), Tick).State;
        state = StepMany(controller, collisionWorld, state, new KinematicCharacterInput(Vector2.Zero, false), 120).State;

        Assert.True(state.IsGrounded);
        AssertFinite(state);
        Assert.InRange(state.Position.Y, -0.001f, 0.02f);
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

    [Fact]
    public void CollisionHeavyMovementDoesNotProduceNonFiniteState()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateCornerMap());
        var controller = new KinematicCharacterController(new KinematicCharacterSettings { WalkSpeed = 18.0f });
        KinematicCharacterState state = new(new Vector3(1.2f, 0.0f, 1.2f), Vector3.Zero, true);

        for (int i = 0; i < 300; i++)
        {
            Vector2 move = i % 2 == 0 ? Vector2.One : new Vector2(1.0f, -0.25f);
            state = controller.Step(collisionWorld, state, new KinematicCharacterInput(move, false), Tick).State;
            AssertFinite(state);
        }

        Assert.True(state.IsGrounded);
        Assert.InRange(state.Position.X, 1.35f, 1.56f);
    }

    [Theory]
    [InlineData(float.NaN, 1.0f)]
    [InlineData(float.PositiveInfinity, 0.0f)]
    [InlineData(0.0f, float.NegativeInfinity)]
    public void NonFiniteMovementInputIsIgnored(float x, float y)
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateFloorMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        KinematicCharacterStepResult result = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(new Vector2(x, y), false),
            Tick);

        Assert.True(result.State.IsGrounded);
        AssertFinite(result.State);
        Assert.InRange(MathF.Abs(result.State.Position.X), 0.0f, 0.001f);
        Assert.InRange(MathF.Abs(result.State.Position.Z), 0.0f, 0.001f);
    }

    [Fact]
    public void CrouchedMovementUsesCrouchedSpeedAndPreservesFeetAndRadius()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateFloorMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState start = new(Vector3.Zero, Vector3.Zero, true);

        KinematicCharacterState state = StepMany(
            controller,
            collisionWorld,
            start,
            new KinematicCharacterInput(new Vector2(1.0f, 0.0f), Jump: false, Crouch: true),
            60).State;

        Assert.Equal(KinematicCharacterStance.Crouched, state.Stance);
        Assert.InRange(state.Position.X, 2.40f, 2.60f);
        Assert.InRange(state.Position.Y, -0.001f, 0.02f);
        Assert.Equal(0.35f, controller.Settings.Radius);
        Assert.Equal(1.1f, controller.Settings.GetHeight(state.Stance));
    }

    [Fact]
    public void AirborneCrouchChangesStanceImmediatelyWithoutMovingFeet()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateFloorMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState start = new(new Vector3(0.0f, 3.0f, 0.0f), Vector3.Zero, false);

        KinematicCharacterStepResult result = controller.Step(
            collisionWorld,
            start,
            new KinematicCharacterInput(Vector2.Zero, Jump: false, Crouch: true),
            Tick);

        Assert.Equal(KinematicCharacterStance.Crouched, result.State.Stance);
        Assert.Equal(start.Position.X, result.State.Position.X);
        Assert.Equal(start.Position.Z, result.State.Position.Z);
        Assert.True(result.State.Position.Y < start.Position.Y);
    }

    [Fact]
    public void JumpIsRejectedWhileCrouchedOrRequestingCrouch()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateFloorMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState grounded = new(Vector3.Zero, Vector3.Zero, true);

        KinematicCharacterStepResult result = controller.Step(
            collisionWorld,
            grounded,
            new KinematicCharacterInput(Vector2.Zero, Jump: true, Crouch: true),
            Tick);

        Assert.False(result.JumpAccepted);
        Assert.Equal(KinematicCharacterStance.Crouched, result.State.Stance);
        Assert.Equal(0.0f, result.State.Velocity.Y, precision: 4);
    }

    [Fact]
    public void StandRequestRemainsCrouchedUnderCeilingAndAutomaticallyStandsAfterClearance()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateCrouchTunnelMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);
        state = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(Vector2.Zero, Jump: false, Crouch: true),
            Tick).State;

        state = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(Vector2.Zero, Jump: false, Crouch: false),
            Tick).State;
        Assert.Equal(KinematicCharacterStance.Crouched, state.Stance);
        Assert.InRange(state.Position.Y, -0.001f, 0.02f);

        state = StepMany(
            controller,
            collisionWorld,
            state,
            new KinematicCharacterInput(new Vector2(1.0f, 0.0f), Jump: false, Crouch: false),
            80).State;

        Assert.Equal(KinematicCharacterStance.Standing, state.Stance);
        Assert.True(state.Position.X > 2.0f);
    }

    [Theory]
    [InlineData(0.0f, 1.0f)]
    [InlineData(1.0f, 1.0f)]
    public void SprintUsesSprintSpeedWithoutDiagonalAmplification(float x, float y)
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateFloorMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        state = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(new Vector2(x, y), Jump: false, Sprint: true),
            Tick).State;

        Assert.True(state.IsSprinting);
        Assert.Equal(7.0f, new Vector2(state.Velocity.X, state.Velocity.Z).Length(), precision: 4);
    }

    [Fact]
    public void CrouchAndBlockedStandRequestRejectSprint()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateCrouchTunnelMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        state = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(Vector2.UnitX, Jump: false, Crouch: true, Sprint: true),
            Tick).State;
        Assert.True(state.IsCrouched);
        Assert.False(state.IsSprinting);

        state = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(Vector2.UnitX, Jump: false, Crouch: false, Sprint: true),
            Tick).State;

        Assert.True(state.IsCrouched);
        Assert.False(state.IsSprinting);
        Assert.Equal(controller.Settings.CrouchedSpeed, MathF.Abs(state.Velocity.X), precision: 4);
    }

    [Fact]
    public void SprintPersistsWhileAirborneWhenIntentRemainsHeld()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateFloorMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(Vector3.Zero, Vector3.Zero, true);

        state = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(Vector2.UnitY, Jump: true, Sprint: true),
            Tick).State;
        Assert.False(state.IsGrounded);
        Assert.True(state.IsSprinting);

        state = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(Vector2.UnitY, Jump: false, Sprint: true),
            Tick).State;

        Assert.False(state.IsGrounded);
        Assert.True(state.IsSprinting);
        Assert.Equal(7.0f, MathF.Abs(state.Velocity.Z), precision: 4);
    }

    [Fact]
    public void CollisionDoesNotClearEffectiveSprintState()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateWallMap());
        var controller = new KinematicCharacterController();
        KinematicCharacterState state = new(new Vector3(1.52f, 0.0f, 0.0f), Vector3.Zero, true);

        state = controller.Step(
            collisionWorld,
            state,
            new KinematicCharacterInput(Vector2.UnitX, Jump: false, Sprint: true),
            Tick).State;

        Assert.True(state.IsSprinting);
        Assert.InRange(state.Position.X, 1.45f, 1.56f);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-0.01f)]
    public void InvalidSprintSpeedIsRejected(float sprintSpeed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KinematicCharacterController(new KinematicCharacterSettings { SprintSpeed = sprintSpeed }));
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

    private static GameMap CreateThinWallMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)),
        Box("thin-wall", new Vector3(2.0f, 1.0f, 0.0f), new Vector3(0.04f, 2.0f, 8.0f)));

    private static GameMap CreateCornerMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)),
        Box("east-wall", new Vector3(2.0f, 1.0f, 0.0f), new Vector3(0.2f, 2.0f, 4.2f)),
        Box("north-wall", new Vector3(0.0f, 1.0f, 2.0f), new Vector3(4.2f, 2.0f, 0.2f)));

    private static GameMap CreateLowStepMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)),
        Box("low-step", new Vector3(0.0f, 0.05f, 1.0f), new Vector3(3.0f, 0.1f, 0.35f)));

    private static GameMap CreateHighStepMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)),
        Box("high-step", new Vector3(0.0f, 0.25f, 1.0f), new Vector3(3.0f, 0.5f, 0.35f)));

    private static GameMap CreateGrayboxStepLowMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)),
        Box("step-low", new Vector3(-3.05f, 0.2f, 0.85f), new Vector3(1.7f, 0.4f, 0.9f)));

    private static GameMap CreateCeilingMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)),
        Box("ceiling", new Vector3(0.0f, 2.5f, 0.0f), new Vector3(4.0f, 0.2f, 4.0f)));

    private static GameMap CreateCrouchTunnelMap() => CreateMap(
        Box("floor", new Vector3(0.0f, -0.1f, 0.0f), new Vector3(20.0f, 0.2f, 20.0f)),
        Box("low-ceiling", new Vector3(0.0f, 1.25f, 0.0f), new Vector3(3.0f, 0.2f, 4.0f)));

    private static GameMap CreateWalkableSlopeMap() => CreateMap(
        Box(
            "walkable-slope",
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(4.0f, 0.2f, 4.0f),
            new Vector3(-25.0f, 0.0f, 0.0f)));

    private static GameMap CreateSteepSlopeMap() => CreateMap(
        Box(
            "steep-slope",
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(4.0f, 0.2f, 4.0f),
            new Vector3(-60.0f, 0.0f, 0.0f)));

    private static void AssertFinite(KinematicCharacterState state)
    {
        AssertFinite(state.Position);
        AssertFinite(state.Velocity);
    }

    private static void AssertFinite(Vector3 vector)
    {
        Assert.True(float.IsFinite(vector.X));
        Assert.True(float.IsFinite(vector.Y));
        Assert.True(float.IsFinite(vector.Z));
    }

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
