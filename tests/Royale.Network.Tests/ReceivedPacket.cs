using Royale.Network;

namespace Royale.Network.Tests;

internal sealed record ReceivedPacket(
    NetworkPeerId PeerId,
    byte[] Payload,
    NetworkDelivery Delivery,
    byte Channel);
