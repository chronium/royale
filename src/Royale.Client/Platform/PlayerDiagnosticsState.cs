using System.Globalization;
using Royale.Client.Gameplay;
using Royale.Simulation;

namespace Royale.Client.Platform;

public readonly record struct PlayerDiagnosticsState(
    int CurrentHealth,
    int MaxHealth,
    bool Alive,
    WeaponFeedbackShot? LastShot)
{
    public string HealthText => $"Health: {CurrentHealth}/{MaxHealth}";

    public string AliveText => $"State: {(Alive ? "alive" : "dead")}";

    public string LastShotText => LastShot is WeaponFeedbackShot shot
        ? $"Last shot: {FormatHitType(shot.HitType)}"
        : "Last shot: none";

    public string HitMarkerText => LastShot is WeaponFeedbackShot shot
        ? $"Hit marker: {(shot.HitMarkerActive ? "active" : "inactive")}"
        : "Hit marker: inactive";

    public string HitIdentityText => LastShot is WeaponFeedbackShot shot
        ? $"Hit id: {shot.TargetId ?? shot.StaticColliderId ?? "-"}"
        : "Hit id: -";

    public string DamageText => LastShot is WeaponFeedbackShot shot
        ? $"Damage: {shot.AppliedDamage}"
        : "Damage: 0";

    public string FeedbackLifetimeText => LastShot is WeaponFeedbackShot shot
        ? string.Create(CultureInfo.InvariantCulture, $"Feedback: {shot.RemainingLifetimeSeconds:0.00}s")
        : "Feedback: 0.00s";

    public static PlayerDiagnosticsState FromPlayer(LocalPlayerController player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return new PlayerDiagnosticsState(
            player.Health.CurrentHealth,
            player.Health.MaxHealth,
            player.Alive,
            player.WeaponFeedback.LastShot);
    }

    private static string FormatHitType(HitscanHitType type) => type switch
    {
        HitscanHitType.None => "miss",
        HitscanHitType.Static => "static",
        HitscanHitType.Target => "target",
        _ => type.ToString(),
    };
}
