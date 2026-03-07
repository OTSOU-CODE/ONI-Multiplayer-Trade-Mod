using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace MultiplayerTradeMod
{
    public class MultiplayerLobbyScreen : MonoBehaviour
    {
        public static MultiplayerLobbyScreen Instance { get; private set; }

        private const float W = 560f;
        private const float H = 450f;

        private GameObject _root;
        private GameObject _mainPanel;
        private GameObject _hostPanel;
        private GameObject _joinPanel;

        private InputField _portInput;
        private InputField _addressInput;

        private Text _statusText;
        private Text _hostStatusLabel;
        private Text _joinStatusLabel;

        private Button _claimButton;
        private Button _copyAddressButton;

        private enum View
        {
            Main,
            Host,
            Join
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            BuildUI();
            SwitchTo(View.Main);
            Show();

            if (PlayitManager.Instance != null)
            {
                PlayitManager.Instance.OnAddressFound += OnPlayitAddressFound;
                PlayitManager.Instance.OnPlayitOutput += OnPlayitOutput;
            }
        }

        private void OnDestroy()
        {
            if (PlayitManager.Instance != null)
            {
                PlayitManager.Instance.OnAddressFound -= OnPlayitAddressFound;
                PlayitManager.Instance.OnPlayitOutput -= OnPlayitOutput;
            }

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Hide();

            RefreshStatus();
        }

        public void Show()
        {
            RefreshStatus();
            if (_root != null)
                _root.SetActive(true);
        }

        public void Hide()
        {
            MultiplayerServerManager.Instance?.StopAll();
            PlayitManager.Instance?.StopTunnel();
            if (_root != null)
                _root.SetActive(false);
        }

        public void HideOnly()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        private void BuildUI()
        {
            var canvasGO = MakeGO("LobbyCanvas", transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var backdrop = MakeGO("Backdrop", canvasGO.transform);
            SetFill(backdrop);
            backdrop.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.60f);

            _root = MakeGO("Window", canvasGO.transform);
            var rootRect = _root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(W, H);
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            _root.AddComponent<Image>().color = new Color(0.09f, 0.10f, 0.13f, 0.97f);

            var outline = _root.AddComponent<Outline>();
            outline.effectColor = new Color(0.40f, 0.45f, 0.55f, 0.80f);
            outline.effectDistance = new Vector2(2f, -2f);

            BuildHeader();
            _mainPanel = BuildMainPanel();
            _hostPanel = BuildHostPanel();
            _joinPanel = BuildJoinPanel();

            SetLayer(canvasGO, LayerMask.NameToLayer("UI"));
        }

        private void BuildHeader()
        {
            var header = MakeGO("Header", _root.transform);
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = Vector2.one;
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = new Vector2(0f, -46f);
            header.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.11f);

            var title = MakeText("Title", header.transform, "MULTIPLAYER TRADE", 16,
                new Color(0.90f, 0.75f, 0.25f), FontStyle.Bold, TextAnchor.MiddleCenter);
            SetFill(title.gameObject, 12f, 0f, -50f, 0f);

            _statusText = MakeText("Status", _root.transform, "", 11,
                new Color(0.5f, 0.8f, 0.5f), FontStyle.Normal, TextAnchor.MiddleCenter);
            var statusRect = _statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = new Vector2(12f, -72f);
            statusRect.offsetMax = new Vector2(-12f, -50f);

            MakeButton("X", _root.transform, new Vector2(-8f, -8f),
                new Vector2(36f, 36f), new Vector2(1f, 1f), new Color(0.55f, 0.12f, 0.12f))
                .onClick.AddListener(Hide);
        }

        private GameObject BuildMainPanel()
        {
            var panel = MakePanel("Main");

            float y = -112f;

            MakeBtn("btn_host", panel.transform, "Host Session",
                new Vector2(0f, y), 270f, 44f, new Color(0.17f, 0.40f, 0.20f))
                .onClick.AddListener(() => SwitchTo(View.Host));

            MakeBtn("btn_join", panel.transform, "Join Session",
                new Vector2(0f, y - 58f), 270f, 44f, new Color(0.15f, 0.25f, 0.45f))
                .onClick.AddListener(() => SwitchTo(View.Join));

            MakeBtn("btn_console", panel.transform, "Show Console Overlay [Ctrl+F9]",
                new Vector2(0f, y - 114f), 270f, 34f, new Color(0.18f, 0.18f, 0.25f))
                .onClick.AddListener(() => MultiplayerConsole.Instance?.SetVisible(true));

            var note = MakeText("Note", panel.transform,
                "Use playit.gg for free tunneling if port forwarding is not available.",
                10, new Color(0.45f, 0.50f, 0.55f), FontStyle.Italic, TextAnchor.MiddleCenter);
            var noteRect = note.GetComponent<RectTransform>();
            noteRect.anchorMin = new Vector2(0f, 1f);
            noteRect.anchorMax = new Vector2(1f, 1f);
            noteRect.offsetMin = new Vector2(20f, y - 165f);
            noteRect.offsetMax = new Vector2(-20f, y - 140f);

            return panel;
        }

        private GameObject BuildHostPanel()
        {
            var panel = MakePanel("Host");

            Label(panel.transform, "HOST A TRADE SESSION", -32f, W - 40f, 28f, 15, Color.white, FontStyle.Bold);
            Label(panel.transform, "Listen port (share your playit.gg address with friends):",
                -80f, W - 40f, 18f, 11, Grey());

            var portRow = Row(panel.transform, -108f, W - 80f, 36f);
            _portInput = MakeInput(portRow.transform, MultiplayerServerManager.DEFAULT_PORT.ToString());

            _hostStatusLabel = Label(panel.transform, "Server not started", -148f, W - 40f, 20f, 11,
                new Color(0.5f, 0.5f, 0.5f)).GetComponent<Text>();

            _claimButton = MakeBtn("claimBtn", panel.transform, "Open playit claim link",
                new Vector2(0f, -176f), 220f, 26f, new Color(0.2f, 0.4f, 0.8f));
            _claimButton.gameObject.SetActive(false);
            _claimButton.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(PlayitManager.Instance?.ClaimLink))
                    Application.OpenURL(PlayitManager.Instance.ClaimLink);
            });

            _copyAddressButton = MakeBtn("copyAddr", panel.transform, "Copy tunnel address",
                new Vector2(0f, -208f), 220f, 26f, new Color(0.25f, 0.45f, 0.25f));
            _copyAddressButton.gameObject.SetActive(false);
            _copyAddressButton.onClick.AddListener(() =>
            {
                string addr = PlayitManager.Instance != null ? PlayitManager.Instance.PublicAddress : string.Empty;
                if (!string.IsNullOrEmpty(addr))
                {
                    GUIUtility.systemCopyBuffer = addr;
                    SetHostStatus("Tunnel address copied: " + addr, new Color(0.4f, 0.9f, 0.4f));
                }
            });

            MakeBtn("startHost", panel.transform, "Start Hosting",
                new Vector2(-72f, -250f), 190f, 40f, new Color(0.17f, 0.40f, 0.20f))
                .onClick.AddListener(StartHosting);

            MakeBtn("stopHost", panel.transform, "Stop",
                new Vector2(72f, -250f), 140f, 40f, new Color(0.40f, 0.15f, 0.15f))
                .onClick.AddListener(() =>
                {
                    MultiplayerServerManager.Instance?.StopAll();
                    PlayitManager.Instance?.StopTunnel();
                    SetHostStatus("Server stopped.", new Color(0.8f, 0.4f, 0.4f));
                    if (_claimButton != null) _claimButton.gameObject.SetActive(false);
                    if (_copyAddressButton != null) _copyAddressButton.gameObject.SetActive(false);
                    RefreshStatus();
                });

            MakeBtn("backHost", panel.transform, "Back",
                new Vector2(0f, -312f), 120f, 30f, DarkBtn())
                .onClick.AddListener(() => SwitchTo(View.Main));

            return panel;
        }

        private GameObject BuildJoinPanel()
        {
            var panel = MakePanel("Join");

            Label(panel.transform, "JOIN A TRADE SESSION", -32f, W - 40f, 28f, 15, Color.white, FontStyle.Bold);

            Label(panel.transform, "1) Ask host for address like us1.playit.gg:12345", -74f, W - 40f, 18f, 10,
                new Color(0.6f, 0.65f, 0.7f));
            Label(panel.transform, "2) Host must click Start Hosting first", -94f, W - 40f, 18f, 10,
                new Color(0.6f, 0.65f, 0.7f));
            Label(panel.transform, "3) Paste address and click Connect", -114f, W - 40f, 18f, 10,
                new Color(0.6f, 0.65f, 0.7f));

            var addrRow = Row(panel.transform, -145f, W - 80f, 36f);
            _addressInput = MakeInput(addrRow.transform, "us1.playit.gg:12345");

            _joinStatusLabel = Label(panel.transform, "Not connected", -191f, W - 40f, 20f, 11,
                new Color(0.5f, 0.5f, 0.5f)).GetComponent<Text>();

            MakeBtn("doJoin", panel.transform, "Connect",
                new Vector2(0f, -228f), 220f, 40f, new Color(0.15f, 0.25f, 0.45f))
                .onClick.AddListener(DoJoin);

            MakeBtn("discoBtn", panel.transform, "Disconnect",
                new Vector2(0f, -278f), 160f, 32f, new Color(0.40f, 0.15f, 0.15f))
                .onClick.AddListener(() =>
                {
                    MultiplayerServerManager.Instance?.StopAll();
                    PlayitManager.Instance?.StopTunnel();
                    SetJoinStatus("Disconnected.", new Color(0.8f, 0.4f, 0.4f));
                    RefreshStatus();
                });

            MakeBtn("backJoin", panel.transform, "Back",
                new Vector2(0f, -315f), 120f, 30f, DarkBtn())
                .onClick.AddListener(() => SwitchTo(View.Main));

            return panel;
        }

        private GameObject MakePanel(string name)
        {
            var panel = MakeGO(name + "Panel", _root.transform);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, -46f);
            return panel;
        }

        private void SwitchTo(View view)
        {
            _mainPanel.SetActive(view == View.Main);
            _hostPanel.SetActive(view == View.Host);
            _joinPanel.SetActive(view == View.Join);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_statusText == null)
                return;

            var mgr = MultiplayerServerManager.Instance;
            if (mgr == null || !mgr.IsConnected)
            {
                _statusText.text = "Not connected";
                _statusText.color = new Color(0.55f, 0.55f, 0.55f);
            }
            else if (mgr.IsHost)
            {
                _statusText.text = "Hosting on " + mgr.ConnectedAddress;
                _statusText.color = new Color(0.4f, 0.9f, 0.4f);
            }
            else
            {
                _statusText.text = "Connected to " + mgr.ConnectedAddress;
                _statusText.color = new Color(0.4f, 0.7f, 1f);
            }
        }

        private void StartHosting()
        {
            var mgr = MultiplayerServerManager.Instance;
            if (mgr == null)
            {
                UIManager.EnsureManagers();
                mgr = MultiplayerServerManager.Instance;
            }

            int port = MultiplayerServerManager.DEFAULT_PORT;
            if (_portInput != null)
            {
                string text = (_portInput.text ?? string.Empty).Trim();
                if (int.TryParse(text, out int parsedPort) && parsedPort > 0 && parsedPort <= 65535)
                    port = parsedPort;
            }

            try
            {
                mgr?.StartServer(port);
                SetHostStatus("Server started on port " + port + ". Starting playit tunnel...",
                    new Color(0.8f, 0.8f, 0.3f));
                PlayitManager.Instance?.StartTunnel(port);
            }
            catch (System.Exception ex)
            {
                SetHostStatus("Failed to start: " + ex.Message, new Color(1f, 0.4f, 0.4f));
            }

            RefreshStatus();
        }

        private void OnPlayitAddressFound(string address)
        {
            SetHostStatus("Tunnel ready. Share this address: " + address, new Color(0.4f, 0.9f, 0.4f));

            if (_claimButton != null)
                _claimButton.gameObject.SetActive(!string.IsNullOrEmpty(PlayitManager.Instance?.ClaimLink));

            if (_copyAddressButton != null)
                _copyAddressButton.gameObject.SetActive(!string.IsNullOrEmpty(address));

            RefreshStatus();
        }

        private void OnPlayitOutput(string line)
        {
            if (PlayitManager.Instance != null && PlayitManager.Instance.IsDownloading)
            {
                SetHostStatus("Downloading playit.exe...", new Color(0.8f, 0.8f, 0.4f));
            }
            else if (!string.IsNullOrEmpty(line) && line.IndexOf("error", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SetHostStatus("playit output: " + line, new Color(1f, 0.6f, 0.3f));
            }
        }

        private void DoJoin()
        {
            string addr = string.Empty;
            if (_addressInput != null)
                addr = (_addressInput.text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(addr))
            {
                SetJoinStatus("Type host address first (example: us1.playit.gg:12345)", new Color(1f, 0.7f, 0.2f));
                return;
            }

            SetJoinStatus("Connecting to " + addr + "...", new Color(0.7f, 0.7f, 1f));
            MultiplayerServerManager.Instance?.JoinServer(addr);
            StartCoroutine(DelayedStatusRefresh(2.5f, addr));
        }

        private IEnumerator DelayedStatusRefresh(float delay, string addr)
        {
            yield return new WaitForSeconds(delay);

            var mgr = MultiplayerServerManager.Instance;
            if (mgr != null && mgr.IsConnected)
            {
                SetJoinStatus("Connected to " + addr + ".", new Color(0.4f, 0.9f, 0.4f));
            }
            else
            {
                SetJoinStatus("Connection failed. Check address and try again.", new Color(1f, 0.4f, 0.4f));
            }

            RefreshStatus();
        }

        private void SetHostStatus(string message, Color color)
        {
            if (_hostStatusLabel != null)
            {
                _hostStatusLabel.text = message;
                _hostStatusLabel.color = color;
            }
        }

        private void SetJoinStatus(string message, Color color)
        {
            if (_joinStatusLabel != null)
            {
                _joinStatusLabel.text = message;
                _joinStatusLabel.color = color;
            }
        }

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (go.GetComponent<RectTransform>() == null)
                go.AddComponent<RectTransform>();
            return go;
        }

        private static void SetFill(GameObject go, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static Text MakeText(string name, Transform parent, string text, int size, Color color,
            FontStyle style, TextAnchor align)
        {
            var go = MakeGO(name, parent);
            var label = go.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = align;
            return label;
        }

        private static Text Label(Transform parent, string text, float y, float w, float h,
            int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var label = MakeText("Label", parent, text, size, color, style, TextAnchor.MiddleCenter);
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(w, h);
            return label;
        }

        private static Button MakeBtn(string name, Transform parent, string label,
            Vector2 pos, float w, float h, Color color)
        {
            var go = MakeGO(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(w, h);

            var image = go.AddComponent<Image>();
            image.color = color;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.highlightedColor = color * 1.2f;
            colors.pressedColor = color * 0.8f;
            button.colors = colors;

            var lbl = MakeText("Label", go.transform, label, 13, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetFill(lbl.gameObject);

            return button;
        }

        private static Button MakeButton(string label, Transform parent, Vector2 pos,
            Vector2 size, Vector2 anchor, Color color)
        {
            var go = MakeGO("Button_" + label, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = color;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var lbl = MakeText("Label", go.transform, label, 18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetFill(lbl.gameObject);

            return button;
        }

        private static GameObject Row(Transform parent, float y, float w, float h)
        {
            var go = MakeGO("Row", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(w, h);
            go.AddComponent<Image>().color = new Color(0.07f, 0.08f, 0.11f);
            return go;
        }

        private static InputField MakeInput(Transform parent, string placeholder)
        {
            var go = MakeGO("Input", parent);
            SetFill(go, 4f, 2f, -4f, -2f);

            var field = go.AddComponent<InputField>();

            var ph = MakeText("Placeholder", go.transform, placeholder, 12,
                new Color(0.4f, 0.4f, 0.4f), FontStyle.Normal, TextAnchor.MiddleLeft);
            SetFill(ph.gameObject, 6f, 0f, -6f, 0f);
            field.placeholder = ph;

            var txt = MakeText("Text", go.transform, string.Empty, 12,
                Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetFill(txt.gameObject, 6f, 0f, -6f, 0f);
            field.textComponent = txt;

            return field;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayer(child.gameObject, layer);
        }

        private static Color Grey()
        {
            return new Color(0.70f, 0.70f, 0.70f);
        }

        private static Color DarkBtn()
        {
            return new Color(0.22f, 0.23f, 0.30f);
        }
    }
}
