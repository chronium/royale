using Royale.Client.Gameplay;

namespace Royale.Client.Platform;

public readonly record struct PlayerDiagnosticsState(
    int CurrentHealth,
    int MaxHealth,
    bool Alive)
{
    public string HealthText => $"Health: {CurrentHealth}/{MaxHealth}";

    public string AliveText => $"State: {(Alive ? "alive" : "dead")}";

    public static PlayerDiagnosticsState FromPlayer(LocalPlayerController player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return new PlayerDiagnosticsState(
            player.Health.CurrentHealth,
            player.Health.MaxHealth,
            player.Alive);
    }
}
