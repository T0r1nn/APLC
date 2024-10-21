using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace APLC;

public class APLCNetworking : NetworkBehaviour
{
    private static APLCNetworking _instance;
    public static APLCNetworking Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<APLCNetworking>();
            return _instance;
        }
        set => _instance = value;
    }
    public static NetworkManager networkManager;
    public static GameObject networkingManagerPrefab;
    public static List<GameObject> queuedNetworkPrefabs = new List<GameObject>();
    public static bool networkHasStarted = false;
    public static GameObject networkGameObject;

    
    
    public override void OnNetworkSpawn()
    {
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            Instance?.gameObject.GetComponent<NetworkObject>().Despawn();
        Instance = this;

        base.OnNetworkSpawn();
    }
    
    public static void RegisterNetworkPrefab(GameObject prefab)
    {
        if (!networkHasStarted)
        {
            queuedNetworkPrefabs.Add(prefab);
        }
    }
    
    internal static void RegisterPrefabs(NetworkManager networkManager)
    {
        List<GameObject> addedNetworkPrefabs = new List<GameObject>();
        foreach (NetworkPrefab networkPrefab in networkManager.NetworkConfig.Prefabs.Prefabs)
        {
            addedNetworkPrefabs.Add(networkPrefab.Prefab);
        }
        foreach (GameObject queuedNetworkPrefab in queuedNetworkPrefabs)
        {
            if (!addedNetworkPrefabs.Contains(queuedNetworkPrefab))
            {
                networkManager.AddNetworkPrefab(queuedNetworkPrefab);
                addedNetworkPrefabs.Add(queuedNetworkPrefab);
            }
        }
        networkHasStarted = true;
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
            new MultiworldHandler(url, port, slot, password);
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
            SendConnectionServerRpc(connection.url, connection.port, connection.slot, connection.password);
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
        Plugin._instance.LogInfo("APLC Networking Started");
    }
}