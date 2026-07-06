namespace Royale.Simulation;

public readonly record struct MapStaticCapsuleCast(float Fraction)
{
    public bool Hit => Fraction < 1.0f;
}
