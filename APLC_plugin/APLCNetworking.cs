using Unity.Netcode;
using UnityEngine;

namespace APLC;

// ReSharper disable once InconsistentNaming
public class APLCNetworking : NetworkBehaviour
{
    private static APLCNetworking _instance;
    public static APLCNetworking Instance
    {
        get
        {
            if (_instance == null)
                _instance = GameObject.FindGameObjectWithTag("APLCNetworking").GetComponent<APLCNetworking>();
            return _instance;
        }
        private set => _instance = value;
    }
    public static GameObject NetworkingManagerPrefab;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        gameObject.name = "APLCNetworkManager";
        Instance = this;
    }

    public void SendConnection(ConnectionInfo info)
    {
        SendConnectionServerRpc(info.URL, info.Port, info.Slot, info.Password);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void SendConnectionServerRpc(string url, int port, string slot, string password)
    {
        Debug.Log("Sending connection to server: " + url + ":" + port);
        SendConnectionClientRpc(url, port, slot, password);
    }

    [ClientRpc]
    public void SendConnectionClientRpc(string url, int port, string slot, string password)
    {
        Debug.Log("Received connection to server: " + url + ":" + port);
        if (MultiworldHandler.Instance == null)
        {
            _ = new MwState(new ConnectionInfo(url, port, slot, password));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestConnectionServerRpc()
    {
        Debug.Log("Requesting connection to server");
        RequestConnectionClientRpc();
    }

    [ClientRpc]
    public void RequestConnectionClientRpc()
    {
        Debug.Log("Received request to connect to server");
        if (MultiworldHandler.Instance != null && GameNetworkManager.Instance.localPlayerController.IsHost)
        {
            MultiworldHandler connection = MultiworldHandler.Instance;
            SendConnection(connection.ApConnectionInfo);
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

    private void Start()
    {
        Plugin.Instance.LogInfo("APLC Networking Started");
    }
}