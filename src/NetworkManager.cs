using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace MultiplayerTradeMod
{
    public class MultiplayerServerManager : MonoBehaviour
    {
        public static MultiplayerServerManager Instance { get; private set; }

        public const int DEFAULT_PORT = 7777;
        public const int CONNECT_TIMEOUT_MS = 10000;
        private const int KEEPALIVE_INTERVAL_SECONDS = 5;
        private const int RECONNECT_DELAY_MS = 3000;
        private const int MAX_RECONNECT = 5;

        public bool IsHost { get; private set; }
        public bool IsConnected { get; private set; }
        public string ConnectedAddress { get; private set; } = string.Empty;
        public string LocalIP => GetLocalIP();
        public int ClientCount
        {
            get { lock (_clientsLock) return _clients.Count; }
        }

        private TcpListener _listener;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _clientsLock = new object();

        private TcpClient _clientSocket;
        private string _reconnectAddress = string.Empty;
        private int _reconnectAttempt;
        private bool _intentionalStop;

        private readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();

        private volatile bool _running;
        private Thread _acceptThread;
        private volatile bool _clientKeepaliveRunning;
        private volatile bool _hostKeepaliveRunning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnDestroy()
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            StopAll();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Debug.LogError("[play.gg][MultiplayerTrade] Unhandled background exception: " + e.ExceptionObject);
        }

        private void Update()
        {
            while (_incoming.TryDequeue(out string msg))
            {
                try
                {
                    HandleMessage(msg);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[play.gg][MultiplayerTrade] HandleMessage crashed: " + ex);
                }
            }
        }

        public void StartServer(int port = DEFAULT_PORT)
        {
            if (port <= 0 || port > 65535)
                port = DEFAULT_PORT;

            StopAll();

            _intentionalStop = false;
            IsHost = true;
            IsConnected = true;
            ConnectedAddress = "localhost:" + port;
            _reconnectAddress = string.Empty;
            _reconnectAttempt = 0;

            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Start();

                _running = true;
                _acceptThread = new Thread(AcceptLoop)
                {
                    IsBackground = true,
                    Name = "MP-AcceptLoop"
                };
                _acceptThread.Start();

                string localIp = GetLocalIP();
                Debug.Log("[play.gg][MultiplayerTrade] Hosting on port " + port + " (local IP: " + localIp + ")");
                MultiplayerConsole.LogStateless(
                    "<color=#88FF88>[Host]</color> Listening on <b>port " + port + "</b>\n" +
                    "  Local IP  : <b>" + localIp + ":" + port + "</b>\n" +
                    "  playit.gg : point your TCP tunnel -> <b>localhost:" + port + "</b>");
                UIManager.Instance?.ShowNotification("Hosting on port " + port);
            }
            catch (Exception ex)
            {
                Debug.LogError("[play.gg][MultiplayerTrade] StartServer failed: " + ex.Message);
                MultiplayerConsole.LogStateless("<color=#FF5555>[Host]</color> Failed to start: " + ex.Message);
                IsConnected = false;
                IsHost = false;
            }
        }

        public void JoinServer(string address)
        {
            string normalizedAddress;
            string parseError;
            if (!TryParseAddress(address, out normalizedAddress, out parseError))
            {
                _incoming.Enqueue("__CONNECT_FAIL__:" + parseError);
                return;
            }

            _running = false;
            _clientKeepaliveRunning = false;
            try { _clientSocket?.Close(); } catch { }
            _clientSocket = null;

            _intentionalStop = false;
            _reconnectAddress = normalizedAddress;
            _reconnectAttempt = 0;
            ConnectedAddress = normalizedAddress;
            IsHost = false;
            IsConnected = false;

            StartConnectAttempt(normalizedAddress);
        }

        public void JoinServer()
        {
            Debug.LogWarning("[play.gg][MultiplayerTrade] JoinServer() called without address.");
        }

        public void StopAll()
        {
            _intentionalStop = true;
            _running = false;
            _clientKeepaliveRunning = false;
            _hostKeepaliveRunning = false;

            try { _listener?.Stop(); } catch { }
            _listener = null;

            lock (_clientsLock)
            {
                foreach (TcpClient c in _clients)
                {
                    try { c.Close(); } catch { }
                }
                _clients.Clear();
            }

            try { _clientSocket?.Close(); } catch { }
            _clientSocket = null;

            IsHost = false;
            IsConnected = false;
            ConnectedAddress = string.Empty;

            _acceptThread?.Join(500);
            _acceptThread = null;
        }

        public void SendTradeMessage(TradeMessage trade)
        {
            if (trade.cargo == null)
                trade.cargo = new List<CargoItem>();

            BroadcastRaw("TRADE_DATA|" + JsonConvert.SerializeObject(trade));
        }

        public void SendChat(string sender, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string cleanSender = (sender ?? "Player").Replace("|", string.Empty).Trim();
            if (cleanSender.Length == 0)
                cleanSender = "Player";

            string cleanMessage = text.Replace("|", string.Empty).Trim();
            if (cleanMessage.Length == 0)
                return;

            BroadcastRaw("CHAT|" + cleanSender + "|" + cleanMessage);
        }

        public void BroadcastRawInternal(string msg)
        {
            BroadcastRaw(msg);
        }

        private bool TryParseAddress(string address, out string normalizedAddress, out string error)
        {
            normalizedAddress = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(address))
            {
                error = "Address is empty. Expected host:port.";
                return false;
            }

            address = address.Trim();

            string host;
            int port;
            if (!TrySplitAddress(address, out host, out port, out error))
                return false;

            normalizedAddress = host + ":" + port;
            return true;
        }

        private bool TrySplitAddress(string address, out string host, out int port, out string error)
        {
            host = string.Empty;
            port = DEFAULT_PORT;
            error = string.Empty;

            int idx = address.LastIndexOf(':');
            if (idx < 0)
            {
                host = address.Trim();
                port = DEFAULT_PORT;
            }
            else
            {
                host = address.Substring(0, idx).Trim();
                string portPart = address.Substring(idx + 1).Trim();

                if (!int.TryParse(portPart, out port) || port <= 0 || port > 65535)
                {
                    error = "Invalid port. Use host:port (port 1-65535).";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                error = "Host is empty. Use host:port.";
                return false;
            }

            return true;
        }

        private void StartConnectAttempt(string address)
        {
            var t = new Thread(() => ConnectThread(address))
            {
                IsBackground = true,
                Name = "MP-ConnectThread"
            };
            t.Start();
        }

        private void ConnectThread(string address)
        {
            string normalizedAddress;
            string parseError;
            if (!TryParseAddress(address, out normalizedAddress, out parseError))
            {
                _incoming.Enqueue("__CONNECT_FAIL__:" + parseError);
                return;
            }

            string host;
            int port;
            if (!TrySplitAddress(normalizedAddress, out host, out port, out parseError))
            {
                _incoming.Enqueue("__CONNECT_FAIL__:" + parseError);
                return;
            }

            _incoming.Enqueue("__CONNECT_LOG__:Connecting to " + host + ":" + port +
                (_reconnectAttempt > 0 ? " (attempt " + (_reconnectAttempt + 1) + "/" + MAX_RECONNECT + ")" : string.Empty) + "...");

            TcpClient tcp = null;
            try
            {
                tcp = new TcpClient();
                var ar = tcp.BeginConnect(host, port, null, null);
                bool ok = ar.AsyncWaitHandle.WaitOne(CONNECT_TIMEOUT_MS, false);
                if (!ok || !tcp.Connected)
                {
                    tcp.Close();
                    throw new TimeoutException("Timed out after " + (CONNECT_TIMEOUT_MS / 1000) + "s");
                }
                tcp.EndConnect(ar);

                if (_intentionalStop)
                {
                    tcp.Close();
                    return;
                }

                _clientSocket = tcp;
                IsConnected = true;
                _running = true;
                _incoming.Enqueue("__CONNECTED__");

                _clientKeepaliveRunning = true;
                new Thread(ClientKeepaliveThread)
                {
                    IsBackground = true,
                    Name = "MP-ClientKeepalive"
                }.Start();

                ClientReceiveLoop(tcp);
            }
            catch (ThreadAbortException)
            {
            }
            catch (Exception ex)
            {
                try { tcp?.Close(); } catch { }
                if (!_intentionalStop)
                    _incoming.Enqueue("__CONNECT_FAIL__:" + ex.Message);
            }
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    if (!_running)
                    {
                        try { client.Close(); } catch { }
                        break;
                    }

                    client.NoDelay = true;

                    lock (_clientsLock)
                    {
                        _clients.Add(client);
                    }

                    SendDirect(client, "HELLO_SERVER|play.gg");

                    string endpoint = client.Client.RemoteEndPoint != null
                        ? client.Client.RemoteEndPoint.ToString()
                        : "?";
                    _incoming.Enqueue("__CLIENT_JOIN__:" + endpoint);

                    if (!_hostKeepaliveRunning)
                    {
                        _hostKeepaliveRunning = true;
                        new Thread(HostKeepaliveThread)
                        {
                            IsBackground = true,
                            Name = "MP-HostKeepalive"
                        }.Start();
                    }

                    var t = new Thread(() => ServerReceiveLoop(client))
                    {
                        IsBackground = true,
                        Name = "MP-ClientLoop"
                    };
                    t.Start();
                }
                catch (SocketException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_running)
                        Debug.LogWarning("[play.gg][MultiplayerTrade] AcceptLoop error: " + ex.Message);
                    break;
                }
            }
        }

        private void ServerReceiveLoop(TcpClient client)
        {
            var sb = new StringBuilder();
            var buf = new byte[65536];

            try
            {
                NetworkStream stream = client.GetStream();
                while (_running && client.Connected)
                {
                    int read;
                    try
                    {
                        read = stream.Read(buf, 0, buf.Length);
                    }
                    catch (IOException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    if (read == 0)
                        break;

                    sb.Append(Encoding.UTF8.GetString(buf, 0, read));
                    DrainFrames(sb);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[play.gg][MultiplayerTrade] ServerReceiveLoop error: " + ex.Message);
            }
            finally
            {
                lock (_clientsLock)
                {
                    _clients.Remove(client);
                }

                try { client.Close(); } catch { }
                _incoming.Enqueue("__CLIENT_LEAVE__");
            }
        }

        private void ClientReceiveLoop(TcpClient client)
        {
            var sb = new StringBuilder();
            var buf = new byte[65536];

            try
            {
                NetworkStream stream = client.GetStream();
                while (_running && client.Connected)
                {
                    int read;
                    try
                    {
                        read = stream.Read(buf, 0, buf.Length);
                    }
                    catch (IOException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    if (read == 0)
                        break;

                    sb.Append(Encoding.UTF8.GetString(buf, 0, read));
                    DrainFrames(sb);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[play.gg][MultiplayerTrade] ClientReceiveLoop error: " + ex.Message);
            }
            finally
            {
                if (!_intentionalStop)
                    _incoming.Enqueue("__DISCONNECTED__");
            }
        }

        private void DrainFrames(StringBuilder sb)
        {
            string data = sb.ToString();
            int idx;
            while ((idx = data.IndexOf('\n')) >= 0)
            {
                string frame = data.Substring(0, idx).Trim();
                data = data.Substring(idx + 1);
                if (frame.Length > 0)
                    _incoming.Enqueue(frame);
            }

            sb.Clear();
            sb.Append(data);
        }

        private void BroadcastRaw(string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return;

            byte[] data = Encoding.UTF8.GetBytes(msg + "\n");

            if (IsHost)
            {
                lock (_clientsLock)
                {
                    var dead = new List<TcpClient>();
                    foreach (TcpClient c in _clients)
                    {
                        try
                        {
                            c.GetStream().Write(data, 0, data.Length);
                        }
                        catch
                        {
                            dead.Add(c);
                        }
                    }

                    foreach (TcpClient d in dead)
                    {
                        _clients.Remove(d);
                        try { d.Close(); } catch { }
                    }
                }
            }
            else
            {
                try
                {
                    _clientSocket?.GetStream().Write(data, 0, data.Length);
                }
                catch
                {
                    HandleDrop();
                }
            }
        }

        private void SendRaw(string msg)
        {
            BroadcastRaw(msg);
        }

        private void SendDirect(TcpClient client, string msg)
        {
            if (client == null)
                return;

            byte[] data = Encoding.UTF8.GetBytes(msg + "\n");
            try
            {
                client.GetStream().Write(data, 0, data.Length);
            }
            catch
            {
            }
        }

        private void ClientKeepaliveThread()
        {
            while (_running && _clientKeepaliveRunning && IsConnected && !IsHost)
            {
                Thread.Sleep(KEEPALIVE_INTERVAL_SECONDS * 1000);
                if (_running && IsConnected && !IsHost)
                    SendRaw("PING|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }

        private void HostKeepaliveThread()
        {
            while (_running && _hostKeepaliveRunning && IsHost)
            {
                Thread.Sleep(KEEPALIVE_INTERVAL_SECONDS * 1000);
                if (_running && IsHost && ClientCount > 0)
                    SendRaw("PING|" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }

        private void HandleDrop()
        {
            if (_intentionalStop || IsHost)
                return;

            IsConnected = false;
            _running = false;
            _clientKeepaliveRunning = false;

            try { _clientSocket?.Close(); } catch { }
            _clientSocket = null;

            if (_reconnectAttempt >= MAX_RECONNECT || string.IsNullOrEmpty(_reconnectAddress))
            {
                MultiplayerConsole.LogStateless("<color=#FF5555>[Net]</color> Gave up after " + MAX_RECONNECT + " reconnect attempts.");
                UIManager.Instance?.ShowNotification("Disconnected - could not reconnect.");
                return;
            }

            _reconnectAttempt++;
            string addr = _reconnectAddress;
            MultiplayerConsole.LogStateless("<color=#FFAA44>[Net]</color> Reconnecting in " + (RECONNECT_DELAY_MS / 1000) + "s...");

            var t = new Thread(() =>
            {
                Thread.Sleep(RECONNECT_DELAY_MS);
                if (!_intentionalStop)
                {
                    _running = true;
                    StartConnectAttempt(addr);
                }
            })
            {
                IsBackground = true,
                Name = "MP-Reconnect"
            };
            t.Start();
        }

        private void HandleMessage(string raw)
        {
            if (raw == "__CONNECTED__")
            {
                _reconnectAttempt = 0;
                IsConnected = true;

                MultiplayerConsole.LogStateless("<color=#88FF88>[play.gg]</color> Connected to host.");
                UIManager.Instance?.ShowNotification("Connected to server.");
                Debug.Log("[play.gg][MultiplayerTrade] Connection successful: " + ConnectedAddress);

                BroadcastRaw("HELLO|" + Environment.MachineName);
                MultiplayerLobbyScreen.Instance?.HideOnly();
                RemoteColonyManager.SendLocalColonyInfo();
                return;
            }

            if (raw == "__DISCONNECTED__")
            {
                MultiplayerConsole.LogStateless("<color=#FF8888>[Net]</color> Disconnected from host.");
                UIManager.Instance?.ShowNotification("Disconnected.");
                HandleDrop();
                return;
            }

            if (raw == "__CLIENT_LEAVE__")
            {
                MultiplayerConsole.LogStateless("<color=#FF8888>[Host]</color> A client disconnected.");
                return;
            }

            if (raw.StartsWith("__CLIENT_JOIN__:", StringComparison.Ordinal))
            {
                string ep = raw.Substring("__CLIENT_JOIN__:".Length);
                MultiplayerConsole.LogStateless("<color=#88FF88>[Host]</color> Player connected from " + ep + ". Total: " + ClientCount);
                UIManager.Instance?.ShowNotification("A player joined.");
                Debug.Log("[play.gg][MultiplayerTrade] Incoming client connected from " + ep);
                return;
            }

            if (raw.StartsWith("__CONNECT_LOG__:", StringComparison.Ordinal))
            {
                MultiplayerConsole.LogStateless("<color=#88AAFF>[Join]</color> " + raw.Substring("__CONNECT_LOG__:".Length));
                return;
            }

            if (raw.StartsWith("__CONNECT_FAIL__:", StringComparison.Ordinal))
            {
                string err = raw.Substring("__CONNECT_FAIL__:".Length);
                MultiplayerConsole.LogStateless("<color=#FF5555>[Join]</color> Failed: " + err);
                UIManager.Instance?.ShowNotification("Connection failed: " + err);
                Debug.LogWarning("[play.gg][MultiplayerTrade] Connection failed: " + err);
                HandleDrop();
                return;
            }

            int pipe = raw.IndexOf('|');
            if (pipe < 0)
                return;

            string type = raw.Substring(0, pipe);
            string payload = raw.Substring(pipe + 1);

            switch (type)
            {
                case "PING":
                    SendRaw("PONG|" + payload);
                    break;

                case "PONG":
                    break;

                case "HELLO_SERVER":
                    MultiplayerConsole.LogStateless("<color=#88AAFF>[play.gg]</color> Host handshake received.");
                    break;

                case "HELLO":
                    if (IsHost)
                    {
                        BroadcastRaw("HELLO_ACK|Welcome! You are connected.");
                        RemoteColonyManager.SendLocalColonyInfo();
                    }
                    break;

                case "HELLO_ACK":
                    Debug.Log("[play.gg][MultiplayerTrade] Server acknowledged: " + payload);
                    break;

                case "COLONY_INFO":
                    RemoteColonyManager.Instance?.ReceiveColonyInfo(payload);
                    break;

                case "CHAT":
                {
                    int cp = payload.IndexOf('|');
                    if (cp >= 0)
                    {
                        string sender = payload.Substring(0, cp);
                        string chatMsg = payload.Substring(cp + 1);
                        MultiplayerChat.Instance?.ReceiveMessage(sender, chatMsg);
                        MultiplayerConsole.LogStateless("<color=#FFD700>[Chat]</color> <b>" + sender + ":</b> " + chatMsg);
                        if (IsHost)
                            BroadcastRaw(raw);
                    }
                    break;
                }

                case "TRADE_DATA":
                    try
                    {
                        TradeMessage trade = JsonConvert.DeserializeObject<TradeMessage>(payload);
                        TradeManager.Instance?.ReceiveIncomingTrade(trade);
                        if (IsHost)
                            BroadcastRaw(raw);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[play.gg][MultiplayerTrade] Bad TRADE_DATA: " + ex.Message);
                    }
                    break;

                default:
                    Debug.Log("[play.gg][MultiplayerTrade] Unknown packet type: " + type);
                    break;
            }
        }

        private static string GetLocalIP()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 53);
                    return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
                }
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }
}
