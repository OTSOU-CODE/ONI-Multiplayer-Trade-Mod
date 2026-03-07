using HarmonyLib;
using KMod;
using UnityEngine;

namespace SimpleUDPChat
{
    public class ChatMod : UserMod2
    {
        internal const string LogPrefix = "[play.gg][SimpleUDPChat]";

        internal static void LogInfo(string message)
        {
            Debug.Log(LogPrefix + " " + message);
        }

        internal static void LogWarning(string message)
        {
            Debug.LogWarning(LogPrefix + " " + message);
        }

        internal static void LogError(string message)
        {
            Debug.LogError(LogPrefix + " " + message);
        }

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            harmony.PatchAll();
            LogInfo("Mod loaded and Harmony patches applied.");
        }
    }
}
