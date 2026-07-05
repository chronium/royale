using Royale.Server;

namespace Royale.Server.Tests;

public sealed class ServerDescriptorTests
{
    [Fact]
    public void ServerSkeletonIsHeadless()
    {
        Assert.True(ServerDescriptor.Create().IsHeadless);
    }
}
