using Unity.Netcode;

namespace APLC;

/**
 * Holds connection information for an Archipelago slot
 */
public struct ConnectionInfo : INetworkSerializable
{
    public string URL;
    public int Port;
    public string Slot;
    public string Password;

    public ConnectionInfo(string url, int port, string slot, string password)
    {
        URL = url;
        Port = port;
        Slot = slot;
        Password = password;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref URL);
        serializer.SerializeValue(ref Port);
        serializer.SerializeValue(ref Slot);
        serializer.SerializeValue(ref Password);
    }
}