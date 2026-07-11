using Royale.Server.Bots;
using Royale.Server.Launch;
using Royale.Server.Match;
using Royale.Server.Networking;
using Royale.Server.Observability;
using Royale.Server.Sessions;
using Royale.Server.Simulation;

namespace Royale.Server.Tests.Launch;

public sealed class ServerDescriptorTests
{
    [Fact]
    public void ServerSkeletonIsHeadless()
    {
        Assert.True(ServerDescriptor.Create().IsHeadless);
    }
}
