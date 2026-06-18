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

    [Rpc(SendTo.ClientsAndHost)]
    public void SetTimeUntilDeadlineRpc(float time)
    {
        TimeOfDay.Instance.timeUntilDeadline = time;
        TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
    }

    private void Start()
    {
        Plugin.Logger.LogInfo("APLC Networking Started");
    }

    /**
     * ClientRpc to kill a player if their steam ID matches the one chosen by the host.
     */
    [Rpc(SendTo.NotMe)]
    public void KillPlayerClientRpc(ulong id)
    {
        Plugin.Logger.LogInfo($"Killing player with ID {id}. It might be me?");
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

    [Rpc(SendTo.Server)]
    public void UseFillerServerRpc(string itemName, ulong clientId)
    {
        FillerItems fillerToUse = MwState.Instance.GetItemMap<FillerItems>(itemName);
        if (fillerToUse.GetReceived() > fillerToUse.GetUsed() && fillerToUse.Use())
        {
            HUDManager.Instance.DisplayTip("APLC", $"Used '{itemName}'.");
            SyncClientFillerUsedRpc(itemName, fillerToUse.GetReceived(), fillerToUse.GetUsed());
        }
        else
        {
            Plugin.Logger.LogDebug($"Player tried to use filler item {itemName}, but there aren't any to use or usage failed.");
            SyncClientFillerUsedRpc(itemName, fillerToUse.GetReceived(), fillerToUse.GetUsed(), successfulUse: false,
                rpcParams: RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

    }

    [Rpc(SendTo.NotMe, AllowTargetOverride = true)]
    public void SyncClientFillerUsedRpc(string itemName, int amountReceived, int amountTotal, bool successfulUse = true, RpcParams rpcParams = default)
    {
        // on clients, update the item's _received and _total
        FillerItems fillerToUpdate = MwState.Instance.GetItemMap<FillerItems>(itemName);
        fillerToUpdate.UpdateUsed(amountReceived, amountTotal);
        if (successfulUse) 
            HUDManager.Instance.DisplayTip("APLC", $"Used '{itemName}'.");
        else 
            HUDManager.Instance.DisplayTip("APLC", $"Could not use '{itemName}'.");
    }
}