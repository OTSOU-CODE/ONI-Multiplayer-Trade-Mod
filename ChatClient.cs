using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace SimpleUDPChat
{
    public class ChatClient
    {
        static UdpClient client;

        static IPEndPoint serverEP;

        public static List<string> messages = new List<string>();

        public static string username = "Player";

        public static void Connect(string address)
        {
            string[] parts = address.Split(':');

            string ip = parts[0];

            int port = 7777;

            if (parts.Length > 1)
                port = int.Parse(parts[1]);

            client = new UdpClient();

            serverEP = new IPEndPoint(Dns.GetHostAddresses(ip)[0], port);

            Thread t = new Thread(ReceiveLoop);
            t.Start();

            Send("joined");
        }

        public static void Send(string msg)
        {
            if (client == null) return;

            byte[] data = Encoding.UTF8.GetBytes(username + ":" + msg);

            client.Send(data, data.Length, serverEP);
        }

        static void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                byte[] data = client.Receive(ref remote);

                string msg = Encoding.UTF8.GetString(data);

                messages.Add(msg);
            }
        }
    }
}