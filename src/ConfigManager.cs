using System.IO;
using Newtonsoft.Json;
using KMod;

namespace MultiplayerTradeMod
{
    public class ModConfig
    {
        public NetworkingConfig networking = new NetworkingConfig();
        public GameplayConfig gameplay = new GameplayConfig();
        public UIConfig ui = new UIConfig();
    }

    public class NetworkingConfig
    {
        public int defaultPort = 7777;
        public int connectionTimeout = 10;
        public int maxPlayers = 4;
        public bool enableUPnP = true;
    }

    public class GameplayConfig
    {
        public float payloadTravelTime = 2.0f;
        public bool allowDuplicantTrading = false;
        public string tradeDistance = "unlimited";
        public string landingAccuracy = "random";
    }

    public class UIConfig
    {
        public string chatKey = "M";
        public string hudPosition = "topRight";
        public bool enableNotifications = true;
        public float notificationDuration = 5.0f;
    }

    public static class ConfigManager
    {
        public static ModConfig Config { get; private set; }
        private static string ConfigPath => Path.Combine(Manager.GetDirectory(), "multiplayer_config.json");

        public static void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                Config = JsonConvert.DeserializeObject<ModConfig>(json) ?? new ModConfig();
            }
            else
            {
                Config = new ModConfig();
                SaveConfig();
            }
        }

        public static void SaveConfig()
        {
            string json = JsonConvert.SerializeObject(Config, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
    }
}
