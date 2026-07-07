using LiteNetLib;

namespace Royale.Network;

internal static class NetworkDeliveryMappings
{
    public static DeliveryMethod ToLiteNetLib(this NetworkDelivery delivery) =>
        delivery switch
        {
            NetworkDelivery.Unreliable => DeliveryMethod.Unreliable,
            NetworkDelivery.ReliableUnordered => DeliveryMethod.ReliableUnordered,
            NetworkDelivery.Sequenced => DeliveryMethod.Sequenced,
            NetworkDelivery.ReliableOrdered => DeliveryMethod.ReliableOrdered,
            NetworkDelivery.ReliableSequenced => DeliveryMethod.ReliableSequenced,
            _ => throw new ArgumentOutOfRangeException(nameof(delivery), delivery, "Unsupported network delivery method."),
        };

    public static NetworkDelivery ToNetworkDelivery(this DeliveryMethod delivery) =>
        delivery switch
        {
            DeliveryMethod.Unreliable => NetworkDelivery.Unreliable,
            DeliveryMethod.ReliableUnordered => NetworkDelivery.ReliableUnordered,
            DeliveryMethod.Sequenced => NetworkDelivery.Sequenced,
            DeliveryMethod.ReliableOrdered => NetworkDelivery.ReliableOrdered,
            DeliveryMethod.ReliableSequenced => NetworkDelivery.ReliableSequenced,
            _ => throw new ArgumentOutOfRangeException(nameof(delivery), delivery, "Unsupported LiteNetLib delivery method."),
        };

    internal static byte ToLiteNetLibValue(NetworkDelivery delivery) => (byte)delivery.ToLiteNetLib();
}
