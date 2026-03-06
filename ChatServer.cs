using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace SimpleUDPChat
{
    public class ChatServer
    {
        static UdpClient server;

        static Dictionary<string, IPEndPoint> clients = new Dictionary<string, IPEndPoint>();

        public static void StartServer()
        {
            Thread t = new Thread(ServerLoop);
            t.Start();
        }

        static void ServerLoop()
        {
            server = new UdpClient(7777);

            while (true)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

                byte[] data = server.Receive(ref remote);

                string msg = Encoding.UTF8.GetString(data);

                string[] parts = msg.Split(new char[] { ':' }, 2);
                string clientId = parts[0];
                string content = parts.Length > 1 ? parts[1] : msg;

                if (!clients.ContainsKey(clientId))
                {
                    clients[clientId] = remote;
                    Broadcast(clientId + " joined the chat");
                }
                else
                {
                    clients[clientId] = remote;
                }

                if (content != "joined")
                    Broadcast(clientId + ": " + content);
            }
        }

        static void Broadcast(string msg)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);

            foreach (var c in clients.Values)
                server.Send(data, data.Length, c);
        }
    }
}