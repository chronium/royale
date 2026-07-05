namespace Royale.Server;

public sealed record ServerDescriptor(bool IsHeadless)
{
    public static ServerDescriptor Create() => new(IsHeadless: true);
}
