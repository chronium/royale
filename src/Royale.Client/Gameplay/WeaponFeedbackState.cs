using System.Numerics;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Client.Gameplay;

public sealed class WeaponFeedbackState
{
    public const float DefaultShotLifetimeSeconds = 3.0f;
    public const float DefaultRecoilKickRadians = MathF.PI / 120.0f;
    public const float DefaultRecoilDecaySeconds = 0.12f;

    private readonly float shotLifetimeSeconds;
    private readonly float recoilKickRadians;
    private readonly float recoilDecaySeconds;
    private WeaponFeedbackShot? activeShot;
    private WeaponFeedbackShot? lastShot;

    public WeaponFeedbackState(
        float shotLifetimeSeconds = DefaultShotLifetimeSeconds,
        float recoilKickRadians = DefaultRecoilKickRadians,
        float recoilDecaySeconds = DefaultRecoilDecaySeconds)
    {
        if (!float.IsFinite(shotLifetimeSeconds) || shotLifetimeSeconds <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(shotLifetimeSeconds), "Shot feedback lifetime must be finite and positive.");

        if (!float.IsFinite(recoilKickRadians) || recoilKickRadians < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(recoilKickRadians), "Recoil kick must be finite and non-negative.");

        if (!float.IsFinite(recoilDecaySeconds) || recoilDecaySeconds <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(recoilDecaySeconds), "Recoil decay must be finite and positive.");

        this.shotLifetimeSeconds = shotLifetimeSeconds;
        this.recoilKickRadians = recoilKickRadians;
        this.recoilDecaySeconds = recoilDecaySeconds;
    }

    public WeaponFeedbackShot? ActiveShot => activeShot;

    public WeaponFeedbackShot? LastShot => lastShot;

    public float RecoilPitchRadians { get; private set; }

    public void EmitShot(HitscanRay ray, HitscanHit hit, DamageResult? damageResult)
    {
        Vector3 end = hit.Hit ? hit.Point : ray.Origin + ray.Translation;
        Vector3? hitPoint = hit.Hit ? hit.Point : null;
        var shot = new WeaponFeedbackShot(
            ray.Origin,
            end,
            hit.Type,
            hitPoint,
            hit.TargetId,
            hit.StaticColliderId,
            damageResult,
            shotLifetimeSeconds,
            shotLifetimeSeconds);

        activeShot = shot;
        lastShot = shot;
        RecoilPitchRadians += recoilKickRadians;
    }

    public void Update(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
            return;

        float delta = (float)Math.Min(deltaSeconds, float.MaxValue);

        if (activeShot is WeaponFeedbackShot shot)
        {
            shot = shot with { RemainingLifetimeSeconds = Math.Max(0.0f, shot.RemainingLifetimeSeconds - delta) };
            activeShot = shot.Active ? shot : null;
            lastShot = shot;
        }

        float recoilDecay = recoilKickRadians * delta / recoilDecaySeconds;
        RecoilPitchRadians = Math.Max(0.0f, RecoilPitchRadians - recoilDecay);
    }

    public void Clear()
    {
        activeShot = null;
        lastShot = null;
        RecoilPitchRadians = 0.0f;
    }
}
