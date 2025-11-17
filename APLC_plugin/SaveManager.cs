namespace APLC;

public class SaveManager
{
    public static void CompleteLocation(string location)
    {
        if (MultiworldHandler.Instance == null)
        {
            string[] currentlyQueued = ES3.KeyExists("QueuedLocations", GameNetworkManager.Instance.currentSaveFileName)
                ? ES3.Load<string[]>("QueuedLocations", GameNetworkManager.Instance.currentSaveFileName)
                : [];

            string[] newQueued = new string[currentlyQueued.Length + 1];

            for (int i = 0; i < currentlyQueued.Length; i++)
            {
                newQueued[i] = currentlyQueued[i];
            }

            ES3.Save("QueuedLocations", newQueued, GameNetworkManager.Instance.currentSaveFileName);
        }
        else
        {
            MultiworldHandler.Instance.CompleteLocation(location);
        }
    }

    public static void SaveData<T>(string name, T data)
    {
        ES3.Save(name, data, GameNetworkManager.Instance.currentSaveFileName);
    }

    public static T GetData<T>(string name)
    {
        return ES3.Load<T>(name, GameNetworkManager.Instance.currentSaveFileName);
    }

    public static T GetData<T>(string name, T defaultValue)
    {
        return ES3.Load<T>(name, GameNetworkManager.Instance.currentSaveFileName, defaultValue);
    }

    public static void SendQueuedLocations()
    {
        if(!ES3.KeyExists("QueuedLocations", GameNetworkManager.Instance.currentSaveFileName)) return;
        string[] queued = ES3.Load<string[]>("QueuedLocations", GameNetworkManager.Instance.currentSaveFileName);

        foreach (var location in queued)
        {
            MultiworldHandler.Instance.CompleteLocation(location);
        }
    }

    public static void Startup()
    {
        Config.SendChatMessagesAsAPChat = GetData<bool>("Config sendapchat", true);
        Config.ShowAPMessagesInChat = GetData<bool>("Config showapchat", true);
        Config.MaxCharactersPerChatMessage = GetData<int>("Config maxchat", 50);
        Config.FillerTriggersInstantly = GetData<bool>("Config fillertrigger", true);
        Config.DeathLink = GetData<bool>("Config deathlink", MultiworldHandler.Instance.GetSlotSetting("deathLink") == 1);

        HUDManager.Instance.chatTextField.characterLimit = Config.MaxCharactersPerChatMessage;
    }

    public static void SaveConfig()
    {
        SaveData("Config sendapchat", Config.SendChatMessagesAsAPChat);
        SaveData("Config showapchat", Config.ShowAPMessagesInChat);
        SaveData("Config maxchat", Config.MaxCharactersPerChatMessage);
        SaveData("Config fillertrigger", Config.FillerTriggersInstantly);
        SaveData("Config deathlink", Config.DeathLink);
    }
}