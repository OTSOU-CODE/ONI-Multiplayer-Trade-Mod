using System.Collections.Generic;
using KSerialization;
using HarmonyLib;

namespace MultiplayerTradeMod
{
    // KMonoBehaviour gives us access to ONI's serializer
    [SerializationConfig(MemberSerialization.OptIn)]
    public class MultiplayerSaveManager : KMonoBehaviour
    {
        public static MultiplayerSaveManager Instance;

        // Tells ONI to save this field specifically to the .sav file
        [Serialize]
        public List<TradeMessage> pendingIncomingPayloads = new List<TradeMessage>();

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Instance = this;
        }

        public void AddPendingPayload(TradeMessage payload)
        {
            pendingIncomingPayloads.Add(payload);
        }

        public void RemovePendingPayload(TradeMessage payload)
        {
            // Simple removal matching sender/time
            pendingIncomingPayloads.RemoveAll(p => p.senderId == payload.senderId && p.sendTime == payload.sendTime);
        }

        public void ProcessLoadedPayloads()
        {
            // Restart coroutines for payloads that haven't arrived yet
            for (int i = 0; i < pendingIncomingPayloads.Count; i++)
            {
                var payload = pendingIncomingPayloads[i];
                // Calculate how much time is remaining based on current cycle/time vs sendTime + delay
                float absoluteArrivalTime = payload.sendTime + payload.arrivalDelay;
                float currentTime = GameClock.Instance != null ? GameClock.Instance.GetTime() : 0;
                
                if (currentTime >= absoluteArrivalTime)
                {
                    // It should have arrived while we were offline!
                    payload.arrivalDelay = 0; // Trigger instantly
                }
                else
                {
                    // Still travelling
                    payload.arrivalDelay = absoluteArrivalTime - currentTime;
                }
                
                pendingIncomingPayloads[i] = payload; // Write the struct back to the list

                // If TradeManager instance exists, restart the arrival coroutine
                if (TradeManager.Instance != null)
                {
                    // Call the public helper we will add to TradeManager
                    TradeManager.Instance.ResumeIncomingTrade(payload);
                }
            }
        }
    }

    // Hook into ONI's save loading to trigger our payload resumption
    [HarmonyPatch(typeof(SaveLoader), "Load", new System.Type[] { typeof(string) })]
    public class SaveLoader_Load_Patch
    {
        public static void Postfix()
        {
            if (MultiplayerSaveManager.Instance != null)
            {
                MultiplayerSaveManager.Instance.ProcessLoadedPayloads();
            }
        }
    }
}
