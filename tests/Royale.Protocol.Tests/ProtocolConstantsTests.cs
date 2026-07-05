using Royale.Protocol;

namespace Royale.Protocol.Tests;

public sealed class ProtocolConstantsTests
{
    [Fact]
    public void DefinesInitialProtocolVersion()
    {
        Assert.Equal(1, ProtocolConstants.Version);
    }
}
