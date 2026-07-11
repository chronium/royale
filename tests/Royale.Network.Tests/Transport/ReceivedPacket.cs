using Royale.Network.Handshake;
using Royale.Network.Input;
using Royale.Network.Simulation;
using Royale.Network.Snapshots;
using Royale.Network.Transport;

namespace Royale.Network.Tests.Transport;

internal sealed record ReceivedPacket(
    NetworkPeerId PeerId,
    byte[] Payload,
    NetworkDelivery Delivery,
    byte Channel);
