using Royale.Protocol;

namespace Royale.Protocol.Tests;

public sealed class ProtocolConstantsTests
{
    [Fact]
    public void DefinesInitialProtocolVersion()
    {
        Assert.Equal(1, ProtocolConstants.Version);
        Assert.Equal(1, ProtocolConstants.ProtocolMajorVersion);
        Assert.Equal(0, ProtocolConstants.ProtocolMinorVersion);
    }

    [Fact]
    public void DefinesPacketHeaderLayoutConstants()
    {
        Assert.Equal(0x4C594F52U, ProtocolConstants.PacketMagic);
        Assert.Equal(29, ProtocolConstants.PacketHeaderSize);
    }

    [Fact]
    public void DefinesDefaultNetworkPort()
    {
        Assert.Equal(7777, ProtocolConstants.DefaultPort);
    }
}
