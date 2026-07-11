namespace Royale.Network.Transport;

public enum NetworkDelivery
{
    Unreliable,
    ReliableUnordered,
    Sequenced,
    ReliableOrdered,
    ReliableSequenced,
}
