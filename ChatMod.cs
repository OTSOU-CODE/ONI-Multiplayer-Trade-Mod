using HarmonyLib;
using KMod;

namespace SimpleUDPChat
{
    public class ChatMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            harmony.PatchAll();
        }
    }
}