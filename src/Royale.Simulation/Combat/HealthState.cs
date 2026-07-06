namespace Royale.Simulation.Combat;

public readonly record struct HealthState
{
    public const int DefaultPlayerMaxHealth = 100;

    public HealthState(int maxHealth, int currentHealth, bool alive)
    {
        if (maxHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHealth), "Max health must be positive.");

        MaxHealth = maxHealth;
        CurrentHealth = Math.Clamp(currentHealth, 0, maxHealth);
        Alive = alive && CurrentHealth > 0;
    }

    public int MaxHealth { get; }

    public int CurrentHealth { get; }

    public bool Alive { get; }

    public static HealthState DefaultPlayer { get; } = new(DefaultPlayerMaxHealth, DefaultPlayerMaxHealth, alive: true);

    public HealthState ApplyDamage(int damage)
    {
        if (!Alive || CurrentHealth <= 0 || damage <= 0)
            return this;

        int nextHealth = Math.Max(0, CurrentHealth - damage);
        return new HealthState(MaxHealth, nextHealth, nextHealth > 0);
    }
}
