using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleUDPChat
{
    [HarmonyPatch(typeof(MainMenu), "OnPrefabInit")]
    public class MenuPatch
    {
        private static void Postfix(MainMenu __instance)
        {
            if (__instance.transform.Find("ChatButton") != null)
                return;

            GameObject button = new GameObject("ChatButton");
            button.transform.SetParent(__instance.transform, false);

            RectTransform rect = button.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 42f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -230f);

            button.AddComponent<CanvasRenderer>();
            Image background = button.AddComponent<Image>();
            background.color = new Color(0.15f, 0.45f, 0.30f, 0.95f);

            Button btn = button.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.18f, 0.52f, 0.34f, 1f);
            colors.highlightedColor = new Color(0.22f, 0.60f, 0.40f, 1f);
            colors.pressedColor = new Color(0.12f, 0.38f, 0.25f, 1f);
            colors.selectedColor = colors.highlightedColor;
            btn.colors = colors;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(button.transform, false);

            textObj.AddComponent<CanvasRenderer>();
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.text = "Play.gg Chat";
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            btn.onClick.AddListener(ChatScreen.Open);
            ChatMod.LogInfo("Main menu Play.gg Chat button created.");
        }
    }
}
