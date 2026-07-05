using Royale.Protocol;

namespace Royale.Protocol.Tests;

public sealed class ProtocolConstantsTests
{
    [Fact]
    public void DefinesInitialProtocolVersion()
    {
        Assert.Equal(1, ProtocolConstants.Version);
    }

    [Fact]
    public void DefinesDefaultNetworkPort()
    {
        Assert.Equal(7777, ProtocolConstants.DefaultPort);
    }
}
