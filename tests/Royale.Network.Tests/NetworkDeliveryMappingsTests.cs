using Royale.Network;

namespace Royale.Network.Tests;

public sealed class NetworkDeliveryMappingsTests
{
    [Theory]
    [InlineData(NetworkDelivery.Unreliable, 4)]
    [InlineData(NetworkDelivery.ReliableUnordered, 0)]
    [InlineData(NetworkDelivery.Sequenced, 1)]
    [InlineData(NetworkDelivery.ReliableOrdered, 2)]
    [InlineData(NetworkDelivery.ReliableSequenced, 3)]
    public void MapsToLiteNetLibDeliveryMethod(NetworkDelivery delivery, byte expectedLiteNetLibValue)
    {
        Assert.Equal(expectedLiteNetLibValue, NetworkDeliveryMappings.ToLiteNetLibValue(delivery));
    }

    [Fact]
    public void RejectsUnknownDelivery()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NetworkDeliveryMappings.ToLiteNetLibValue((NetworkDelivery)999));
    }
}
