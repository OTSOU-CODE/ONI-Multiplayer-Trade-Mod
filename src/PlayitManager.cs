using UnityEngine;
using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Concurrent;

namespace MultiplayerTradeMod
{
    public class PlayitManager : MonoBehaviour
    {
        public static PlayitManager Instance { get; private set; }

        private const string PLAYIT_URL = "https://github.com/playit-cloud/playit-agent/releases/latest/download/playit-windows-x86_64.exe";

        private string _playitPath;
        private Process _process;
        private Thread _downloadThread;
        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        public bool IsRunning { get; private set; }
        public bool IsDownloading { get; private set; }
        public string PublicAddress { get; private set; } = string.Empty;
        public string ClaimLink { get; private set; } = string.Empty;

        public Action<string> OnPlayitOutput;
        public Action<string> OnAddressFound;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            string modRoot = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            _playitPath = Path.Combine(modRoot, "playit.exe");
        }

        private void Update()
        {
            while (_mainThreadActions.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[play.gg][MultiplayerTrade] Main-thread playit action failed: " + ex.Message);
                }
            }
        }

        private void OnDestroy()
        {
            StopTunnel();
            if (Instance == this)
                Instance = null;
        }

        public void StartTunnel(int localPort)
        {
            if (localPort <= 0 || localPort > 65535)
                localPort = MultiplayerServerManager.DEFAULT_PORT;

            StopTunnel();
            PublicAddress = string.Empty;
            ClaimLink = string.Empty;

            if (!File.Exists(_playitPath))
            {
                IsDownloading = true;
                Debug.Log("[play.gg][MultiplayerTrade] Downloading playit.exe...");
                _downloadThread = new Thread(() => DownloadAndStart(localPort))
                {
                    IsBackground = true,
                    Name = "Playit-Download"
                };
                _downloadThread.Start();
                return;
            }

            LaunchProcess(localPort);
        }

        public void StopTunnel()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch
            {
            }

            _process = null;
            IsRunning = false;
            IsDownloading = false;
            PublicAddress = string.Empty;
            ClaimLink = string.Empty;
        }

        private void DownloadAndStart(int localPort)
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.DownloadFile(PLAYIT_URL, _playitPath);
                }

                _mainThreadActions.Enqueue(() =>
                {
                    IsDownloading = false;
                    Debug.Log("[play.gg][MultiplayerTrade] playit.exe downloaded.");
                    LaunchProcess(localPort);
                });
            }
            catch (Exception ex)
            {
                _mainThreadActions.Enqueue(() =>
                {
                    IsDownloading = false;
                    IsRunning = false;
                    Debug.LogError("[play.gg][MultiplayerTrade] playit download failed: " + ex.Message);
                    OnPlayitOutput?.Invoke("playit download failed: " + ex.Message);
                });
            }
        }

        private void LaunchProcess(int localPort)
        {
            try
            {
                string modRoot = Path.GetDirectoryName(_playitPath);
                string tomlPath = Path.Combine(modRoot, "playit.toml");

                string tomlContent =
                    "[tunnels.default]\n" +
                    "proto = \"tcp\"\n" +
                    "local_port = " + localPort + "\n";
                File.WriteAllText(tomlPath, tomlContent);

                var psi = new ProcessStartInfo
                {
                    FileName = _playitPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = modRoot
                };

                psi.EnvironmentVariables["PLAYIT_NO_INTERACT"] = "true";

                _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _process.OutputDataReceived += OnOutputData;
                _process.ErrorDataReceived += OnOutputData;
                _process.Exited += (_, __) =>
                {
                    _mainThreadActions.Enqueue(() =>
                    {
                        IsRunning = false;
                        OnPlayitOutput?.Invoke("playit tunnel process exited.");
                    });
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                IsRunning = true;
                Debug.Log("[play.gg][MultiplayerTrade] playit tunnel process started.");
            }
            catch (Exception ex)
            {
                IsRunning = false;
                Debug.LogError("[play.gg][MultiplayerTrade] playit launch failed: " + ex.Message);
            }
        }

        private void OnOutputData(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            string line = Regex.Replace(e.Data, @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", string.Empty);

            var claimMatch = Regex.Match(line, @"claim\s+link:\s*(https://playit\.gg/claim/[0-9a-fA-F]+)", RegexOptions.IgnoreCase);
            if (claimMatch.Success)
            {
                string claim = claimMatch.Groups[1].Value;
                _mainThreadActions.Enqueue(() =>
                {
                    ClaimLink = claim;
                    Debug.Log("[play.gg][MultiplayerTrade] playit claim link: " + ClaimLink);
                });
            }

            var tunnelMatch = Regex.Match(line, @"([a-zA-Z0-9\-\.]+\.(?:ply\.gg|playit\.gg):\d+)", RegexOptions.IgnoreCase);
            if (tunnelMatch.Success)
            {
                string addr = tunnelMatch.Groups[1].Value;
                _mainThreadActions.Enqueue(() =>
                {
                    if (!string.Equals(PublicAddress, addr, StringComparison.OrdinalIgnoreCase))
                    {
                        PublicAddress = addr;
                        Debug.Log("[play.gg][MultiplayerTrade] tunnel address allocated: " + PublicAddress);
                        OnAddressFound?.Invoke(PublicAddress);
                    }
                });
            }

            _mainThreadActions.Enqueue(() => OnPlayitOutput?.Invoke(line));
        }
    }
}
