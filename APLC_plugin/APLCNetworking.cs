using System.Linq;
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

    /**
     * ClientRpc to kill a player if their steam ID matches the one chosen by the host.
     */
    [Rpc(SendTo.NotMe)]
    public void KillPlayerClientRpc(ulong id)
    {
        Plugin.Instance.LogInfo($"Killing player with ID {id}. It might be me?");
        bool markedForDeath = GameNetworkManager.Instance.disableSteam ?
            (GameNetworkManager.Instance.localPlayerController == StartOfRound.Instance.allPlayerScripts.Where(player => !player.isPlayerDead).ToArray()[id]) :
            (GameNetworkManager.Instance.localPlayerController.playerSteamId == id);    // all players have steamID 0 in LAN mode, so we have to use the index instead
        if (!markedForDeath) return;
        GameNetworkManager.Instance.localPlayerController.KillPlayer(Vector3.forward, true, CauseOfDeath.Blast);
        MwState.WaitingForDeath = false;
        MwState.DLMessage = "";
        if (StartOfRound.Instance.allPlayersDead)
        {
            MwState.Instance.IgnoreDL = true;
        }
    }

    /**
     * ClientRpc to sync the scrap total to clients.
     */
    [Rpc(SendTo.NotMe)]
    public void AddCollectathonScrapClientRpc(int amount)
    {
        MwState.Instance.IncrementScrapCollected(amount);
    }

    [Rpc(SendTo.Server)]
    public void SyncConfigServerRpc()
    {
        SyncConfigClientRpc(
            Config.MaxCharactersPerChatMessage,
            Config.FillerTriggersInstantly,
            Config.DeathLink);
    }

    [Rpc(SendTo.NotMe)]
    public void SyncConfigClientRpc(int maxChars, bool fillerInstant, bool deathLink)
    {
        Config.MaxCharactersPerChatMessage = maxChars;
        Config.FillerTriggersInstantly = fillerInstant;
        Config.DeathLink = deathLink;
        HUDManager.Instance.chatTextField.characterLimit = Config.MaxCharactersPerChatMessage;
    }
}