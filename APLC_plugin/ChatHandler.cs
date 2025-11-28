namespace APLC;

/**
 * Handles the processing of chat messages for Archipelago commands.
 */
public static class ChatHandler
{
    //Archipelago connection
    //Stops the weird bug where messages send up to four times
    private static string _lastChatMessage = "";
    private const string LastPrevMessage = "";

    /**
     * Checks if a message should be sent to the AP chat
     */
    public static bool IsChatMessage(string message)
    {
        return !(message[0] == '/' || (message.Length >= 3 && message[..3] == "AP:"));
    }

    //The url of the AP world
    private static string _url;
    //The port of the AP world
    private static int _port;
    //The slot name of the AP world
    private static string _slot;
    //The password of the AP world(null if no password)
    private static string _password;

    //True if waiting for user to enter slot name
    private static bool _waitingForSlot;
    //True if waiting for user to enter password
    private static bool _waitingForPassword;

    /**
     * Handles the entering of /connect, /disconnect, and the slot and password
     */
    public static bool HandleCommands(string message, string user)
    {
        if (_lastChatMessage == message)
            return true;

        _lastChatMessage = message;
        
        if(message.Length >= 3 && message[..3] == "AP:") return true;
        var tokens = message.Split(" ");
        switch (tokens[0])
        {
            case "/connect":
            {
                //If host, run connect, otherwise ask for connection info from host
                if (GameNetworkManager.Instance.localPlayerController.IsHost && MultiworldHandler.Instance == null)
                {
                    if (tokens.Length < 2) return false;
                    var connectionInfo = tokens[1].Split(":");
                    if (connectionInfo.Length < 2) return false;
                    _url = connectionInfo[0];
                    _port = int.Parse(connectionInfo[1]);
                    _waitingForSlot = true;
                    SendMessage("AP: Please enter your slot name:");
                }
                else
                {
                    SendConnectionRequest();
                }

                return true;
            }
            case "/disconnect" when MultiworldHandler.Instance == null:
                return true;
            case "/disconnect":
                MultiworldHandler.Instance.Disconnect();
                MultiworldHandler.Instance = null;
                SendMessage("AP: Disconnect successful, please join a new save if you are doing a different Multiworld");
                return true;
            case "/resync":
                MultiworldHandler.Instance.Refresh(new AplcEventArgs(MultiworldHandler.Instance.GetReceivedItems()));
                SendMessage("AP: Resyncing items");
                return true;
            default:
            {
                if (_waitingForSlot)
                {
                    _slot = message;
                    _waitingForSlot = false;
                    _waitingForPassword = true;
                    SendMessage("AP: Please enter your password(Enter the letter n if there isn't a password):");
                    return true;
                }
                if (_waitingForPassword)
                {
                    _password = message == "n" ? "" : message;

                    _waitingForPassword = false;
                    
                    ConnectionInfo info = new ConnectionInfo(_url, _port, _slot, _password);
                    
                    _ = new MwState(info);
                    
                    APLCNetworking.Instance.SendConnection(info);
                    return true;
                }

                break;
            }
        }

        return false;
    }
    
     public static bool PreventMultisendBug(string chatMessage)
    {
        if (LastPrevMessage == chatMessage) return false;
        if (_lastChatMessage == chatMessage) return false;
        return true;
    }

    /**
     * Sends messages to the game chat
     */
    public static void SendMessage(string message)
    {
        if (message.Length >= 3 && message[..3] == "AP:" && !GameNetworkManager.Instance.isHostingGame) return;
        HUDManager.Instance.AddTextToChatOnServer(message, -1);
    }

    public static void SetConnectionInfo(string url, int port, string slot, string password)
    {
        _url = url;
        _port = port;
        _slot = slot;
        _password = password;
    }
    
    /**
     * Sends a request to the host to connect to the AP world
     */
    private static void SendConnectionRequest()
    {
        APLCNetworking.Instance.RequestConnectionServerRpc();
    }
}