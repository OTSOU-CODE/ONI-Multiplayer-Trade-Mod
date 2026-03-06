using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleUDPChat
{
    [HarmonyPatch(typeof(MainMenu), "OnPrefabInit")]
    public class MenuPatch
    {
        static void Postfix(MainMenu __instance)
        {
            GameObject button = new GameObject("ChatButton");

            button.transform.SetParent(__instance.transform);

            RectTransform rect = button.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 40);
            rect.anchoredPosition = new Vector2(0, -200);

            button.AddComponent<Image>();
            Button btn = button.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(button.transform);

            Text text = textObj.AddComponent<Text>();
            text.text = "Chat";
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.alignment = TextAnchor.MiddleCenter;

            btn.onClick.AddListener(() =>
            {
                ChatScreen.Open();
            });
        }
    }
}