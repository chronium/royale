namespace Royale.Server.Launch;

public sealed record ServerDescriptor(bool IsHeadless)
{
    public static ServerDescriptor Create() => new(IsHeadless: true);
}
