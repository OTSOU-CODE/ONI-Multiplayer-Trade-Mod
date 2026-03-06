using UnityEngine;

namespace SimpleUDPChat
{
    public class ChatScreen : MonoBehaviour
    {
        static ChatScreen instance;

        string address = "127.0.0.1:7777";
        string username = "Player1";
        string message = "";

        Vector2 scroll;

        public static void Open()
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("ChatScreen");
                instance = obj.AddComponent<ChatScreen>();
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(100, 100, 500, 400), "Chat", "Window");

            if (GUILayout.Button("Host"))
                ChatServer.StartServer();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:");
            username = GUILayout.TextField(username, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            address = GUILayout.TextField(address);

            if (GUILayout.Button("Join"))
            {
                ChatClient.username = username;
                ChatClient.Connect(address);
            }

            GUILayout.EndHorizontal();

            GUILayout.Label("Messages");

            scroll = GUILayout.BeginScrollView(scroll);

            foreach (string m in ChatClient.messages)
                GUILayout.Label(m);

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();

            message = GUILayout.TextField(message);

            if (GUILayout.Button("Send"))
            {
                ChatClient.Send(message);
                message = "";
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}