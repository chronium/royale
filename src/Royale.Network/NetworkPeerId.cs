namespace Royale.Network;

public readonly record struct NetworkPeerId
{
    public NetworkPeerId(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Network peer id must be non-negative.");
        }

        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString();
}
