using Archipelago.MultiClient.Net.Helpers;
using Unity.Netcode;
using UnityEngine;

namespace APLC;

public class APLCNetworking : NetworkBehaviour
{
    public static APLCNetworking Instance { get; private set; }
    private int p = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            if (Instance.gameObject != null)
                Destroy(Instance.gameObject);
            else
                Destroy(Instance);
            Instance = this;
        }

        Debug.Log("Successfully loaded APLC Networking");
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendConnectionRpc(string url, int port, string slot, string password)
    {
        ReceiveConnectionRpc(url, port, slot, password);
    }

    [ClientRpc]
    public void ReceiveConnectionRpc(string url, int port, string slot, string password)
    {
        if (MultiworldHandler.Instance == null)
        {
            new MultiworldHandler(url, port, slot, password);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestConnectionRpc()
    {
        ReceiveRequestConnectionRpc();
    }

    [ClientRpc]
    public void ReceiveRequestConnectionRpc()
    {
        if (MultiworldHandler.Instance != null && GameNetworkManager.Instance.localPlayerController.IsHost)
        {
            MultiworldHandler connection = MultiworldHandler.Instance;
            SendConnectionRpc(connection.url, connection.port, connection.slot, connection.password);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetTimeUntilDeadlineServerRpc(float time)
    {
        SetTimeUntilDeadlineClientRpc(time);
    }

    [ClientRpc]
    public void SetTimeUntilDeadlineClientRpc(float time)
    {
        TimeOfDay.Instance.timeUntilDeadline = time;
    }
    
    void Update()
    {
    }
}