using System.Numerics;

namespace Royale.Simulation.Movement;

public readonly record struct KinematicCharacterStepResult(
    KinematicCharacterState State,
    Vector3 Displacement,
    bool JumpAccepted,
    bool HitCeiling,
    bool Stepped,
    int SlideIterations);
