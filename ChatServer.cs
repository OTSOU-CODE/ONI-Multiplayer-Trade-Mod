using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SimpleUDPChat
{
    public class ChatServer
    {
        private const int Port = 7777;

        private static readonly object ClientLock = new object();

        private static UdpClient server;
        private static Thread serverThread;
        private static bool running;

        private static readonly Dictionary<string, IPEndPoint> clients = new Dictionary<string, IPEndPoint>();

        public static bool IsRunning
        {
            get { return running; }
        }

        public static void StartServer()
        {
            if (running)
            {
                ChatMod.LogInfo("Host is already running on UDP port " + Port + ".");
                ChatClient.AddLocalNotice("Host already running on port " + Port + ".");
                return;
            }

            running = true;
            serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "SimpleUDPChat-ServerLoop"
            };
            serverThread.Start();
        }

        public static void StopServer()
        {
            if (!running)
                return;

            running = false;

            if (server != null)
            {
                try
                {
                    server.Close();
                }
                catch
                {
                }
            }

            server = null;

            if (serverThread != null && serverThread.IsAlive)
            {
                try
                {
                    serverThread.Join(100);
                }
                catch
                {
                }
            }

            serverThread = null;

            lock (ClientLock)
            {
                clients.Clear();
            }

            ChatMod.LogInfo("Host stopped.");
            ChatClient.AddLocalNotice("Host stopped.");
        }

        private static void ServerLoop()
        {
            try
            {
                server = new UdpClient(Port);
                ChatMod.LogInfo("Host started on UDP port " + Port + ".");
                ChatClient.AddLocalNotice("Host started on port " + Port + ".");

                while (running)
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data;

                    try
                    {
                        data = server.Receive(ref remote);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException ex)
                    {
                        if (!running)
                            break;

                        ChatMod.LogWarning("Host receive error: " + ex.SocketErrorCode);
                        continue;
                    }

                    string msg = Encoding.UTF8.GetString(data);

                    string[] parts = msg.Split(new[] { ':' }, 2);
                    string clientId = parts[0];
                    string content = parts.Length > 1 ? parts[1] : msg;

                    if (string.IsNullOrWhiteSpace(clientId))
                    {
                        ChatMod.LogWarning("Ignoring malformed packet from " + remote + ".");
                        continue;
                    }

                    bool isNewClient;
                    lock (ClientLock)
                    {
                        isNewClient = !clients.ContainsKey(clientId);
                        clients[clientId] = remote;
                    }

                    bool isJoinPacket = string.Equals(content, "joined", StringComparison.OrdinalIgnoreCase);
                    if (isJoinPacket)
                    {
                        SendDirect(ChatClient.ConnectAckPrefix + "play.gg host", remote);

                        if (isNewClient)
                            Broadcast("[server] " + clientId + " joined the chat.");

                        ChatMod.LogInfo("Connection accepted for '" + clientId + "' from " + remote + ".");
                    }

                    if (!isJoinPacket)
                        Broadcast(clientId + ": " + content);
                }
            }
            catch (Exception ex)
            {
                ChatMod.LogError("Host crashed: " + ex);
                ChatClient.AddLocalNotice("Host error. Check log for details.");
            }
            finally
            {
                running = false;

                if (server != null)
                {
                    try
                    {
                        server.Close();
                    }
                    catch
                    {
                    }
                }

                server = null;

                lock (ClientLock)
                {
                    clients.Clear();
                }
            }
        }

        private static void SendDirect(string msg, IPEndPoint endpoint)
        {
            if (server == null || endpoint == null)
                return;

            byte[] data = Encoding.UTF8.GetBytes(msg);
            server.Send(data, data.Length, endpoint);
        }

        private static void Broadcast(string msg)
        {
            if (server == null)
                return;

            byte[] data = Encoding.UTF8.GetBytes(msg);

            List<IPEndPoint> snapshot;
            lock (ClientLock)
            {
                snapshot = new List<IPEndPoint>(clients.Values);
            }

            foreach (IPEndPoint endpoint in snapshot)
            {
                try
                {
                    server.Send(data, data.Length, endpoint);
                }
                catch (Exception ex)
                {
                    ChatMod.LogWarning("Failed to send packet to " + endpoint + ": " + ex.Message);
                }
            }
        }
    }
}
