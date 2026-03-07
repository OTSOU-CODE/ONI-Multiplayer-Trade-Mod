using HarmonyLib;
using KMod;
using System;
using System.Reflection;
using UnityEngine;

namespace MultiplayerTradeMod
{
    public class MultiplayerCore : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

            try
            {
                ConfigManager.LoadConfig();
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Debug.Log("[play.gg][MultiplayerTrade] Mod loaded, config parsed, Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[play.gg][MultiplayerTrade] OnLoad failed: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(PauseScreen), "OnSpawn")]
    public class PauseScreenMultiplayerPatch
    {
        public static void Postfix(PauseScreen __instance)
        {
            UIManager.AddMultiplayerButton(__instance);
        }
    }

    /* Trade Machine building patches - disabled until kanim assets are included
    [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
    public class GeneratedBuildings_LoadGeneratedBuildings_Patch
    {
        public static void Prefix()
        {
            Strings.Add($"STRINGS.BUILDINGS.PREFABS.{TradeMachineConfig.ID.ToUpperInvariant()}.NAME", TradeMachineConfig.DisplayName);
            Strings.Add($"STRINGS.BUILDINGS.PREFABS.{TradeMachineConfig.ID.ToUpperInvariant()}.DESC", TradeMachineConfig.Description);
            Strings.Add($"STRINGS.BUILDINGS.PREFABS.{TradeMachineConfig.ID.ToUpperInvariant()}.EFFECT", TradeMachineConfig.Effect);

            ModUtil.AddBuildingToPlanScreen("Base", TradeMachineConfig.ID);
        }
    }

    [HarmonyPatch(typeof(Db), "Initialize")]
    public class Db_Initialize_Patch
    {
        public static void Postfix()
        {
            Db.Get().Techs.Get("SolidTransport").unlockedItemIDs.Add(TradeMachineConfig.ID);
        }
    }
    */
}
