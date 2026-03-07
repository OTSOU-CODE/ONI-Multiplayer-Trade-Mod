using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SimpleUDPChat
{
    public class ChatClient
    {
        private const int DefaultPort = 7777;
        private const int ReceiveTimeoutMs = 1000;
        private const int ConnectTimeoutMs = 5000;
        private const int MaxMessages = 200;
        internal const string ConnectAckPrefix = "__PLAYGG_CONNECTED__:";

        private static readonly object MessageLock = new object();
        private static readonly object StateLock = new object();

        private static readonly List<string> messages = new List<string>();

        private static UdpClient client;
        private static IPEndPoint serverEP;
        private static Thread receiveThread;
        private static bool receiveLoopRunning;

        public static string username = "Player";

        public static bool IsConnected { get; private set; }
        public static bool IsConnecting { get; private set; }
        public static string ConnectionStatus { get; private set; } = "Not connected";
        public static string LastError { get; private set; } = string.Empty;

        public static void Connect(string address)
        {
            Disconnect(false);

            string host;
            int port;
            string parseError;
            if (!TryParseAddress(address, out host, out port, out parseError))
            {
                SetError(parseError);
                return;
            }

            try
            {
                IPAddress resolvedAddress = ResolveAddress(host);

                client = new UdpClient();
                client.Client.ReceiveTimeout = ReceiveTimeoutMs;
                serverEP = new IPEndPoint(resolvedAddress, port);

                receiveLoopRunning = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "SimpleUDPChat-ReceiveLoop"
                };
                receiveThread.Start();

                lock (StateLock)
                {
                    IsConnected = false;
                    IsConnecting = true;
                    ConnectionStatus = "Connecting...";
                    LastError = string.Empty;
                }

                AddSystemMessage("Connecting to " + serverEP + " ...");
                ChatMod.LogInfo("Attempting connection to " + serverEP + " as '" + username + "'.");

                SendRaw(username + ":joined");
                StartConnectionTimeoutWatch();
            }
            catch (Exception ex)
            {
                SetError("Connection failed: " + ex.Message);
                ChatMod.LogError("Connection setup failed: " + ex);
                Disconnect(false);
            }
        }

        public static void Disconnect(bool announce = true)
        {
            bool wasConnectedOrConnecting;

            lock (StateLock)
            {
                wasConnectedOrConnecting = IsConnected || IsConnecting;
                IsConnected = false;
                IsConnecting = false;
                ConnectionStatus = "Not connected";
                LastError = string.Empty;
            }

            receiveLoopRunning = false;

            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                }
            }

            client = null;
            serverEP = null;

            if (receiveThread != null && receiveThread.IsAlive)
            {
                try
                {
                    receiveThread.Join(100);
                }
                catch
                {
                }
            }

            receiveThread = null;

            if (announce && wasConnectedOrConnecting)
            {
                AddSystemMessage("Disconnected.");
                ChatMod.LogInfo("Disconnected from server.");
            }
        }

        public static void Send(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg))
                return;

            if (client == null)
            {
                SetError("Not connected.");
                return;
            }

            if (!IsConnected)
            {
                AddSystemMessage("Waiting for connection confirmation...");
                return;
            }

            try
            {
                SendRaw(username + ":" + msg.Trim());
            }
            catch (Exception ex)
            {
                SetError("Send failed: " + ex.Message);
                ChatMod.LogError("Send failed: " + ex);
            }
        }

        public static List<string> GetMessagesSnapshot()
        {
            lock (MessageLock)
            {
                return new List<string>(messages);
            }
        }

        public static void ClearMessages()
        {
            lock (MessageLock)
            {
                messages.Clear();
            }
        }

        public static void AddLocalNotice(string message)
        {
            AddSystemMessage(message);
        }

        private static void StartConnectionTimeoutWatch()
        {
            var timeoutThread = new Thread(() =>
            {
                Thread.Sleep(ConnectTimeoutMs);

                bool shouldTimeout;
                lock (StateLock)
                {
                    shouldTimeout = IsConnecting && !IsConnected;
                    if (shouldTimeout)
                    {
                        IsConnecting = false;
                        ConnectionStatus = "Connection timeout";
                        LastError = "No server acknowledgement received.";
                    }
                }

                if (shouldTimeout)
                {
                    AddSystemMessage("Connection timeout. Check host IP/port and ensure host is running.");
                    ChatMod.LogWarning("Connection timeout waiting for server ack.");
                }
            })
            {
                IsBackground = true,
                Name = "SimpleUDPChat-ConnectTimeout"
            };

            timeoutThread.Start();
        }

        private static bool TryParseAddress(string address, out string host, out int port, out string error)
        {
            host = string.Empty;
            port = DefaultPort;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(address))
            {
                error = "Server address cannot be empty.";
                return false;
            }

            address = address.Trim();

            int separator = address.LastIndexOf(':');
            if (separator <= 0 || separator == address.Length - 1)
            {
                host = address;
                return true;
            }

            host = address.Substring(0, separator);
            string portPart = address.Substring(separator + 1);

            int parsedPort;
            if (!int.TryParse(portPart, out parsedPort) || parsedPort <= 0 || parsedPort > 65535)
            {
                error = "Invalid port in address. Expected host:port (port 1-65535).";
                return false;
            }

            port = parsedPort;
            return true;
        }

        private static IPAddress ResolveAddress(string host)
        {
            IPAddress parsed;
            if (IPAddress.TryParse(host, out parsed))
                return parsed;

            IPAddress[] addresses = Dns.GetHostAddresses(host);
            if (addresses == null || addresses.Length == 0)
                throw new InvalidOperationException("No IP address found for host '" + host + "'.");

            IPAddress ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            return ipv4 ?? addresses[0];
        }

        private static void SendRaw(string payload)
        {
            if (client == null || serverEP == null)
                return;

            byte[] data = Encoding.UTF8.GetBytes(payload);
            client.Send(data, data.Length, serverEP);
        }

        private static void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (receiveLoopRunning)
            {
                try
                {
                    byte[] data = client.Receive(ref remote);
                    string msg = Encoding.UTF8.GetString(data);

                    if (msg.StartsWith(ConnectAckPrefix, StringComparison.Ordinal))
                    {
                        string source = msg.Substring(ConnectAckPrefix.Length);

                        lock (StateLock)
                        {
                            IsConnected = true;
                            IsConnecting = false;
                            ConnectionStatus = "Connected";
                            LastError = string.Empty;
                        }

                        AddSystemMessage("Connected successfully to " + source + ".");
                        ChatMod.LogInfo("Connection successful with " + remote + " (source: " + source + ").");
                        continue;
                    }

                    AddChatMessage(msg);
                }
                catch (SocketException ex)
                {
                    if (!receiveLoopRunning)
                        break;

                    if (ex.SocketErrorCode == SocketError.TimedOut)
                        continue;

                    SetError("Network error: " + ex.SocketErrorCode);
                    ChatMod.LogWarning("Receive loop socket error: " + ex.SocketErrorCode);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!receiveLoopRunning)
                        break;

                    SetError("Receive failed: " + ex.Message);
                    ChatMod.LogError("Receive loop failed: " + ex);
                }
            }
        }

        private static void AddSystemMessage(string message)
        {
            AddChatMessage("[system] " + message);
        }

        private static void AddChatMessage(string message)
        {
            lock (MessageLock)
            {
                messages.Add(message);
                if (messages.Count > MaxMessages)
                {
                    int removeCount = messages.Count - MaxMessages;
                    messages.RemoveRange(0, removeCount);
                }
            }
        }

        private static void SetError(string error)
        {
            lock (StateLock)
            {
                LastError = error;
                if (!IsConnected)
                {
                    IsConnecting = false;
                    ConnectionStatus = "Not connected";
                }
            }

            AddSystemMessage(error);
            ChatMod.LogWarning(error);
        }
    }
}
