using System.Collections.Generic;
using UnityEngine;

namespace SimpleUDPChat
{
    public class ChatScreen : MonoBehaviour
    {
        private static ChatScreen instance;

        private Rect windowRect = new Rect(80, 80, 620, 500);
        private const int WindowId = 985421;

        private string address = "127.0.0.1:7777";
        private string username = "Player1";
        private string message = string.Empty;

        private Vector2 scroll;

        private GUIStyle headerStyle;
        private GUIStyle statusStyle;
        private GUIStyle messageStyle;
        private GUIStyle warningStyle;

        public static void Open()
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("ChatScreen");
                instance = obj.AddComponent<ChatScreen>();
                DontDestroyOnLoad(obj);
            }
            else
            {
                instance.enabled = true;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnGUI()
        {
            EnsureStyles();
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, "play.gg Chat");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Connection", headerStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(55));
            username = GUILayout.TextField(username, GUILayout.Width(140));
            GUILayout.Label("Server", GUILayout.Width(55));
            address = GUILayout.TextField(address);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (!ChatServer.IsRunning)
            {
                if (GUILayout.Button("Host"))
                {
                    ChatServer.StartServer();
                    if (!ChatClient.IsConnected && !ChatClient.IsConnecting)
                    {
                        ChatClient.username = string.IsNullOrWhiteSpace(username) ? "Player" : username.Trim();
                        ChatClient.Connect("127.0.0.1:7777");
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Stop Host"))
                {
                    ChatServer.StopServer();
                }
            }

            if (GUILayout.Button("Join"))
            {
                ChatClient.username = string.IsNullOrWhiteSpace(username) ? "Player" : username.Trim();
                ChatClient.Connect(address);
            }

            GUI.enabled = ChatClient.IsConnected || ChatClient.IsConnecting;
            if (GUILayout.Button("Disconnect"))
                ChatClient.Disconnect();
            GUI.enabled = true;

            if (GUILayout.Button("Clear Log", GUILayout.Width(90)))
                ChatClient.ClearMessages();
            GUILayout.EndHorizontal();

            Color previousColor = GUI.color;
            GUI.color = ChatClient.IsConnected ? new Color(0.65f, 1.0f, 0.65f) : (ChatClient.IsConnecting ? new Color(1.0f, 0.95f, 0.60f) : new Color(1.0f, 0.75f, 0.75f));
            GUILayout.Label("Status: " + ChatClient.ConnectionStatus, statusStyle);
            GUI.color = previousColor;

            if (!string.IsNullOrEmpty(ChatClient.LastError))
                GUILayout.Label("Error: " + ChatClient.LastError, warningStyle);

            GUILayout.Space(8);
            GUILayout.Label("Messages", headerStyle);

            List<string> snapshot = ChatClient.GetMessagesSnapshot();
            scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.Height(260));
            foreach (string line in snapshot)
                GUILayout.Label(line, messageStyle);
            GUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
                scroll.y = float.MaxValue;

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUI.enabled = ChatClient.IsConnected;
            message = GUILayout.TextField(message);
            if (GUILayout.Button("Send", GUILayout.Width(90)))
            {
                ChatClient.Send(message);
                message = string.Empty;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (!ChatClient.IsConnected)
                GUILayout.Label("Tip: wait for the green Connected status before sending.", warningStyle);

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private void EnsureStyles()
        {
            if (headerStyle != null)
                return;

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.fontSize = 14;

            statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.fontStyle = FontStyle.Bold;
            statusStyle.fontSize = 12;

            messageStyle = new GUIStyle(GUI.skin.label);
            messageStyle.wordWrap = true;

            warningStyle = new GUIStyle(GUI.skin.label);
            warningStyle.normal.textColor = new Color(1f, 0.85f, 0.55f);
        }
    }
}
