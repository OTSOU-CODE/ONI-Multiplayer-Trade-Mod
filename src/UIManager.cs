using System;
using HarmonyLib;
using UnityEngine;

namespace MultiplayerTradeMod
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        public void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        internal static void EnsureManagers()
        {
            GameObject root = GameObject.Find("MultiplayerManagers");
            if (root == null)
            {
                root = new GameObject("MultiplayerManagers");
                Object.DontDestroyOnLoad(root);
            }

            EnsureComponent<UIManager>(root);
            EnsureComponent<TradeManager>(root);
            EnsureComponent<MultiplayerSaveManager>(root);
            EnsureComponent<MultiplayerServerManager>(root);
            EnsureComponent<PlayitManager>(root);
            EnsureComponent<RemoteColonyManager>(root);

            EnsureSingletonObject<MultiplayerConsole>("MultiplayerConsole");
            EnsureSingletonObject<MultiplayerChat>("MultiplayerChat");
        }

        public static void AddMultiplayerButton(PauseScreen pauseScreen)
        {
            if (pauseScreen == null)
                return;

            KButton[] allButtons = pauseScreen.GetComponentsInChildren<KButton>(true);
            for (int i = 0; i < allButtons.Length; i++)
            {
                if (allButtons[i] != null && string.Equals(allButtons[i].gameObject.name, "MultiplayerButton", StringComparison.Ordinal))
                    return;
            }

            KButton targetButton = null;

            for (int i = 0; i < allButtons.Length; i++)
            {
                KButton btn = allButtons[i];
                if (btn == null)
                    continue;

                string buttonName = btn.gameObject.name;
                if (buttonName.IndexOf("Options", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    buttonName.IndexOf("Save", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetButton = btn;
                    break;
                }
            }

            if (targetButton == null)
            {
                LocText[] allTexts = pauseScreen.GetComponentsInChildren<LocText>(true);
                for (int i = 0; i < allTexts.Length; i++)
                {
                    LocText text = allTexts[i];
                    if (text == null || string.IsNullOrEmpty(text.text))
                        continue;

                    string upper = text.text.ToUpperInvariant();
                    if (upper.Contains("OPTIONS") || upper.Contains("SAVE"))
                    {
                        targetButton = text.GetComponentInParent<KButton>();
                        if (targetButton != null)
                            break;
                    }
                }
            }

            if (targetButton == null)
            {
                Debug.LogWarning("[play.gg][MultiplayerTrade] Could not find pause-menu anchor button for Multiplayer.");
                return;
            }

            Transform buttonContainer = targetButton.transform.parent;
            if (buttonContainer == null)
                return;

            if (buttonContainer.Find("MultiplayerButton") != null)
                return;

            GameObject multiplayerButton = GameObject.Instantiate(targetButton.gameObject, buttonContainer);
            multiplayerButton.name = "MultiplayerButton";

            LocText locText = multiplayerButton.GetComponentInChildren<LocText>();
            if (locText != null)
                locText.text = "MULTIPLAYER";

            KButton kButton = multiplayerButton.GetComponent<KButton>();
            if (kButton != null)
            {
                kButton.ClearOnClick();
                kButton.onClick += OpenMultiplayerMenu;
            }

            multiplayerButton.transform.SetSiblingIndex(targetButton.transform.GetSiblingIndex() + 1);
        }

        public static void OpenMultiplayerMenu()
        {
            EnsureManagers();
            Debug.Log("[play.gg][MultiplayerTrade] Opening lobby screen.");

            if (MultiplayerLobbyScreen.Instance == null)
            {
                GameObject go = new GameObject("MultiplayerLobbyScreen");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<MultiplayerLobbyScreen>();
            }
            else
            {
                MultiplayerLobbyScreen.Instance.Show();
            }
        }

        public void ShowNotification(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            Debug.Log("[play.gg][MultiplayerTrade] " + message);
            MultiplayerConsole.Instance?.Log(message);
        }

        public void PlaySound(string soundName)
        {
            Debug.Log("[play.gg][MultiplayerTrade] Play sound: " + soundName);
        }

        public void AddChatMessage(string senderName, string message)
        {
            string formatted = string.Format("<color=#FFD700>{0}</color>: {1}", senderName, message);
            Debug.Log(string.Format("[play.gg][MultiplayerTrade][Chat] {0}: {1}", senderName, message));
            MultiplayerConsole.Instance?.Log(formatted);
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static void EnsureSingletonObject<T>(string objectName) where T : Component
        {
            T instance = Object.FindObjectOfType<T>();
            if (instance != null)
                return;

            GameObject go = GameObject.Find(objectName);
            if (go == null)
                go = new GameObject(objectName);

            if (go.GetComponent<T>() == null)
                go.AddComponent<T>();

            Object.DontDestroyOnLoad(go);
        }
    }

    [HarmonyPatch(typeof(Game), "OnPrefabInit")]
    public class GameInitUIPatch
    {
        public static void Postfix(Game __instance)
        {
            UIManager.EnsureManagers();
        }
    }
}
