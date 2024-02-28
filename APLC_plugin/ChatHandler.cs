using UnityEngine.UIElements;

namespace APLC;

public class ChatHandler
{
    /**
     * Checks if the message is a hidden connection packet, if not then let the message display
     */
    public static bool AllowChatMessageToSend(string message)
    {
        return message.Length < 2 || message[..2] != "__";
    }

    //Archipelago connection
    private static MultiworldHandler _multiworldHandler;
    //Stops the weird bug where messages send up to four times
    private static string _lastChatMessage = "";
    private static string _lastPrevMessage = "";
    
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
    private static bool _waitingForSlot = false;
    //True if waiting for user to enter password
    private static bool _waitingForPassword = false;

    //Handles the entering of /connect, /disconnect, and the slot and password
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
                if (GameNetworkManager.Instance.localPlayerController.IsHost)
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
            case "/disconnect" when _multiworldHandler == null:
                return true;
            case "/disconnect":
                _multiworldHandler.Disconnect();
                _multiworldHandler = null;
                SendMessage("AP: Disconnect successful, please join a new save if you are doing a different Multiworld");
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
                    _password = message == "n" ? null : message;

                    _waitingForPassword = false;
                    _multiworldHandler = new MultiworldHandler(_url, _port, _slot, _password);
                    HUDManager.Instance.AddTextToChatOnServer(
                        $"__APConnection: {_url} {_port} {_slot} {_password}");
                    return true;
                }

                break;
            }
        }

        return false;
    }

    /**
     * Handles the connecting of other players, with the __APConnection message and the __RequestAPConnection message
     */
    public static bool HandleConnectingOthers(string chatMessage)
    {
        if (_lastPrevMessage == chatMessage) return false;
        if (_lastChatMessage == chatMessage) return false;

        _lastPrevMessage = chatMessage;
        var tokens = chatMessage.Split(" ");
        if (tokens[0] == "__APConnection:" && !GameNetworkManager.Instance.isHostingGame &&
            MultiworldHandler.Instance == null)
        {
            string url = tokens[1];
            int port = int.Parse(tokens[2]);
            string slotName = "";
            for (var i = 3; i < tokens.Length-1; i++)
            {
                slotName += tokens[i];
                if (i < tokens.Length - 2) slotName += " ";
            }

            string password = tokens[^1];
            _multiworldHandler = new MultiworldHandler(url, port, slotName, password);
        }

        if (tokens[0] == "__RequestAPConnection:" && GameNetworkManager.Instance.isHostingGame &&
            MultiworldHandler.Instance != null)
            HUDManager.Instance.AddTextToChatOnServer(
                $"__APConnection: {_url} {_port} {_slot} {_password}");

        if (tokens[0] == "__updateTime" && !GameNetworkManager.Instance.isHostingGame)
        {
            TimeOfDay.Instance.timeUntilDeadline = float.Parse(tokens[1]);
        }

        return true;
    }

    /**
     * Sends messages to the game chat
     */
    public static void SendMessage(string message)
    {
        if (message.Length >= 3 && message[..3] == "AP:" && !GameNetworkManager.Instance.isHostingGame) return;
        HUDManager.Instance.AddTextToChatOnServer(message);
    }
    
    /**
     * Sends a request to the host to connect to the AP world
     */
    private static void SendConnectionRequest()
    {
        SendMessage("__RequestAPConnection:");
    }
}