namespace Royale.Network;

public readonly record struct NetworkEndpoint
{
    public NetworkEndpoint(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Network endpoint host must not be empty.", nameof(host));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Network endpoint port must be between 1 and 65535.");
        }

        Host = host;
        Port = port;
    }

    public string Host { get; }

    public int Port { get; }

    public override string ToString() => $"{Host}:{Port}";
}
