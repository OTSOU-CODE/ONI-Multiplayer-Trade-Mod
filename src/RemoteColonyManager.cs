using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MultiplayerTradeMod
{
    /// <summary>
    /// Collects local colony data and syncs it to the remote player via COLONY_INFO packets.
    /// Stores the latest received remote colony info and fires OnRemoteColonyUpdated.
    /// </summary>
    public class RemoteColonyManager : MonoBehaviour
    {
        public static RemoteColonyManager Instance { get; private set; }

        public RemoteColonyInfo Current { get; private set; }
        public System.Action<RemoteColonyInfo> OnRemoteColonyUpdated;

        private const float SYNC_INTERVAL = 60f;
        private float _syncTimer = 15f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            var mgr = MultiplayerServerManager.Instance;
            if (mgr == null || !mgr.IsConnected) return;
            _syncTimer -= Time.unscaledDeltaTime;
            if (_syncTimer <= 0f)
            {
                _syncTimer = SYNC_INTERVAL;
                SendLocalColonyInfo();
            }
        }

        public void ReceiveColonyInfo(string json)
        {
            var info = RemoteColonyInfo.Deserialize(json);
            if (info == null) return;
            Current = info;
            OnRemoteColonyUpdated?.Invoke(info);
            Debug.Log($"[Multiplayer] Colony info received: {info.WorldName} (Cycle {info.Cycle})");
        }

        public static void SendLocalColonyInfo()
        {
            try
            {
                var info = new RemoteColonyInfo();

                // World name/type
                WorldContainer world = null;
                try
                {
                    var cm = ClusterManager.Instance;
                    if (cm != null) world = cm.GetWorld(cm.activeWorldId);
                }
                catch { }
                if (world == null)
                    try { world = ClusterManager.Instance?.GetWorld(0); } catch { }

                info.WorldName = world?.worldName ?? SaveGame.Instance?.name ?? "Unknown Colony";
                info.WorldType = world?.worldType.ToString() ?? "Unknown";

                // Cycle & dupes
                try { info.Cycle = (int)GameClock.Instance.GetCycle(); } catch { }
                try { info.DupeCount = Components.MinionIdentities.Count; } catch { }

                // Rockets
                try { info.RocketCount = SpacecraftManager.instance?.GetSpacecraft()?.Count ?? 0; } catch { }

                // Stored resources from WorldInventory
                float totalMass = 0f;
                var resources = new List<ResourceEntry>();
                try
                {
                    if (world != null && DiscoveredResources.Instance != null)
                    {
                        var inv = world.GetComponent<WorldInventory>();
                        if (inv != null)
                        {
                            foreach (var tag in DiscoveredResources.Instance.GetDiscovered())
                            {
                                float kg = inv.GetAmount(tag, false);
                                if (kg > 0.1f)
                                {
                                    resources.Add(new ResourceEntry { Tag = tag.ToString(), Kg = kg });
                                    totalMass += kg;
                                }
                            }
                            resources.Sort((a, b) => b.Kg.CompareTo(a.Kg));
                            if (resources.Count > 6) resources.RemoveRange(6, resources.Count - 6);
                        }
                    }
                }
                catch { }
                info.StoredMassKg = totalMass;
                info.TopResources = resources;

                // Geysers via reflection (avoids API mismatch issues)
                var geysers = new List<GeyserEntry>();
                try
                {
                    foreach (var geyser in Object.FindObjectsOfType<Geyser>())
                    {
                        if (geyser == null) continue;
                        int gWorld = -1;
                        try { gWorld = geyser.GetMyWorldId(); } catch { }
                        if (gWorld != (world?.id ?? 0)) continue;

                        string typeName = "Geyser";
                        try
                        {
                            var cfgF = geyser.GetType().GetField("configuration",
                                System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Instance);
                            var cfg = cfgF?.GetValue(geyser);
                            var tidF = cfg?.GetType().GetField("typeId",
                                System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Instance);
                            typeName = tidF?.GetValue(cfg)?.ToString() ?? "Geyser";
                        }
                        catch { }
                        geysers.Add(new GeyserEntry { Type = typeName, AvgKg = 0f });
                    }
                }
                catch { }
                info.Geysers = geysers;

                MultiplayerServerManager.Instance?.BroadcastRawInternal("COLONY_INFO|" + info.Serialize());
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Multiplayer] SendLocalColonyInfo failed: " + ex.Message);
            }
        }
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    public class RemoteColonyInfo
    {
        public string WorldName   { get; set; } = "";
        public string WorldType   { get; set; } = "";
        public int    Cycle       { get; set; }
        public int    DupeCount   { get; set; }
        public int    RocketCount { get; set; }
        public float  StoredMassKg { get; set; }
        public List<ResourceEntry> TopResources { get; set; } = new List<ResourceEntry>();
        public List<GeyserEntry>   Geysers      { get; set; } = new List<GeyserEntry>();

        public string Serialize() => JsonConvert.SerializeObject(this);
        public static RemoteColonyInfo Deserialize(string json)
        {
            try { return JsonConvert.DeserializeObject<RemoteColonyInfo>(json); }
            catch { return null; }
        }
    }

    public class ResourceEntry { public string Tag { get; set; } public float Kg { get; set; } }
    public class GeyserEntry   { public string Type { get; set; } public float AvgKg { get; set; } }
}
