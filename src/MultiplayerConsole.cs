using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Text;

namespace MultiplayerTradeMod
{
    /// <summary>
    /// Improved in-game multiplayer console HUD.
    /// • Draggable via title bar
    /// • Color-coded INFO / WARN / ERROR entries
    /// • Command input bar (prefix with /)
    /// • Clear and Copy buttons
    /// • Ctrl+F9 toggle
    /// </summary>
    public class MultiplayerConsole : MonoBehaviour
    {
        public static MultiplayerConsole Instance { get; private set; }

        // ── Layout ───────────────────────────────────────────────────────────────
        private const float W         = 480f;
        private const float H_FULL    = 280f;
        private const float H_MINI    = 28f;
        private const float TITLE_H   = 28f;
        private const float CMD_H     = 30f;

        // ── State ────────────────────────────────────────────────────────────────
        private bool _visible    = true;
        private bool _minimized  = false;
        private bool _dragging   = false;
        private Vector2 _dragOff;

        // ── UI refs ──────────────────────────────────────────────────────────────
        private RectTransform _panelRect;
        private Text          _logText;
        private ScrollRect    _scroll;
        private InputField    _cmdInput;
        private Text          _minBtnLabel;
        private Text          _statusBadge;

        // ── Log buffer ───────────────────────────────────────────────────────────
        private readonly List<string>   _lines    = new List<string>();
        private static   List<string>   _pending  = new List<string>();
        private const    int            MAX_LINES = 120;

        // ════════════════════════════════════════════════════════════════════════
        //  Unity lifecycle
        // ════════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            BuildUI();
            LogInternal("<color=#FFD700>[Console]</color> Ready. <color=#888>Ctrl+F9 toggle · /help for commands</color>", false);

            foreach (var p in _pending) LogInternal(p, false);
            _pending.Clear();
            Redraw();
        }

        private void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.F9))
            {
                _visible = !_visible;
                if (_panelRect) _panelRect.gameObject.SetActive(_visible);
            }

            // Command submit on Enter
            if (_visible && !_minimized && _cmdInput != null && _cmdInput.isFocused)
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    RunCommand();

            // Drag
            if (_dragging && _panelRect != null)
            {
                Vector2 mp;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panelRect.parent as RectTransform, Input.mousePosition, null, out mp);
                _panelRect.anchoredPosition = mp + _dragOff;
            }

            // Refresh connection badge ~once per second
            _badgeTick -= Time.unscaledDeltaTime;
            if (_badgeTick <= 0f) { _badgeTick = 1f; UpdateBadge(); }
        }

        private float _badgeTick = 0f;

        // ════════════════════════════════════════════════════════════════════════
        //  Public API
        // ════════════════════════════════════════════════════════════════════════

        public void SetVisible(bool show)
        {
            _visible = show;
            if (_panelRect) _panelRect.gameObject.SetActive(_visible);
        }

        public static void LogStateless(string msg)
        {
            if (Instance != null)
                Instance.Log(msg);
            else
                _pending.Add(Stamp(msg));
        }

        public void Log(string msg)
        {
            LogInternal(Stamp(msg), true);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Commands
        // ════════════════════════════════════════════════════════════════════════

        private void RunCommand()
        {
            if (_cmdInput == null) return;
            string raw = (_cmdInput.text ?? "").Trim();
            _cmdInput.text = "";
            if (raw.Length == 0) return;

            Log("<color=#AAAAFF>» " + raw + "</color>");

            string cmd = raw.StartsWith("/") ? raw.Substring(1).ToLower() : raw.ToLower();
            string[] parts = cmd.Split(new char[]{' '}, 2);

            switch (parts[0])
            {
                case "help":
                    Log("<color=#88FF88>Commands:</color> /help  /clear  /status  /ping &lt;addr&gt;  /host [port]  /join &lt;addr:port&gt;  /stop");
                    break;

                case "clear":
                    _lines.Clear();
                    Redraw();
                    break;

                case "status":
                {
                    var mgr = MultiplayerServerManager.Instance;
                    if (mgr == null || !mgr.IsConnected)
                        Log("<color=#888>Not connected.</color>");
                    else if (mgr.IsHost)
                        Log($"<color=#88FF88>HOSTING</color> on {mgr.ConnectedAddress}  clients: {mgr.ClientCount}  local IP: {mgr.LocalIP}");
                    else
                        Log($"<color=#88AAFF>CONNECTED</color> to {mgr.ConnectedAddress}");
                    break;
                }

                case "host":
                {
                    int port = MultiplayerServerManager.DEFAULT_PORT;
                    if (parts.Length > 1) int.TryParse(parts[1], out port);
                    MultiplayerServerManager.Instance?.StartServer(port);
                    break;
                }

                case "join":
                    if (parts.Length > 1)
                        MultiplayerServerManager.Instance?.JoinServer(parts[1]);
                    else
                        Log("<color=#FF8888>Usage: /join address:port</color>");
                    break;

                case "stop":
                    MultiplayerServerManager.Instance?.StopAll();
                    Log("<color=#FF8888>Server/connection stopped.</color>");
                    break;

                case "ping":
                    if (parts.Length > 1)
                        Log($"<color=#888>Pinging {parts[1]}… (check the game log for TCP result)</color>");
                    else
                        Log("<color=#FF8888>Usage: /ping address:port</color>");
                    break;

                default:
                    Log($"<color=#FF8888>Unknown command: {parts[0]}. Type /help.</color>");
                    break;
            }

            _cmdInput.ActivateInputField();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  UI construction
        // ════════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            // Canvas
            var cvGO = new GameObject("ConsoleCanvas");
            cvGO.transform.SetParent(transform, false);
            var cv = cvGO.AddComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 9000;
            cvGO.AddComponent<CanvasScaler>();
            cvGO.AddComponent<GraphicRaycaster>();

            // Panel — anchored top-center
            var panelGO = new GameObject("ConsolePanel");
            panelGO.transform.SetParent(cvGO.transform, false);
            _panelRect = panelGO.AddComponent<RectTransform>();
            _panelRect.anchorMin = _panelRect.anchorMax = new Vector2(0.5f, 1f);
            _panelRect.pivot     = new Vector2(0.5f, 1f);
            _panelRect.sizeDelta = new Vector2(W, H_FULL + TITLE_H + CMD_H);
            _panelRect.anchoredPosition = new Vector2(0f, -8f);
            panelGO.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

            // ── Title bar ────────────────────────────────────────────────────────
            var titleGO = Rect("Title", panelGO.transform);
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);  titleRT.anchorMax = Vector2.one;
            titleRT.offsetMin = Vector2.zero;        titleRT.offsetMax = new Vector2(0, -TITLE_H);
            titleGO.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.22f);

            // Title text
            var ttGO = Rect("TitleText", titleGO.transform);
            var ttRT = ttGO.GetComponent<RectTransform>();
            ttRT.anchorMin = Vector2.zero; ttRT.anchorMax = Vector2.one;
            ttRT.offsetMin = new Vector2(8, 0); ttRT.offsetMax = new Vector2(-120, 0);
            var title = ttGO.AddComponent<Text>();
            title.text      = "📡  MULTIPLAYER CONSOLE  <color=#555>Ctrl+F9</color>";
            title.font      = Arial(); title.fontSize = 12; title.fontStyle = FontStyle.Bold;
            title.color     = new Color(0.8f, 0.85f, 1f);
            title.alignment = TextAnchor.MiddleLeft; title.supportRichText = true;

            // Connection badge (right of title)
            var bdgGO = Rect("Badge", titleGO.transform);
            var bdgRT = bdgGO.GetComponent<RectTransform>();
            bdgRT.anchorMin = Vector2.zero; bdgRT.anchorMax = Vector2.one;
            bdgRT.offsetMin = new Vector2(0, 0); bdgRT.offsetMax = new Vector2(-60, 0);
            _statusBadge = bdgGO.AddComponent<Text>();
            _statusBadge.font      = Arial(); _statusBadge.fontSize = 10;
            _statusBadge.color     = Color.grey;
            _statusBadge.alignment = TextAnchor.MiddleRight;

            // Clear button
            MakeTitleBtn("Clr", titleGO.transform, new Vector2(-52, 0), new Color(0.25f, 0.25f, 0.38f),
                () => { _lines.Clear(); Redraw(); });

            // Minimize button
            var minGO = MakeTitleBtn("Min", titleGO.transform, new Vector2(-24, 0), new Color(0.28f, 0.28f, 0.42f),
                ToggleMinimize);
            _minBtnLabel = minGO.GetComponentInChildren<Text>();

            // Drag events on title bar
            AddDrag(titleGO);

            // ── Log scroll area ──────────────────────────────────────────────────
            var svGO = Rect("Scroll", panelGO.transform);
            var svRT = svGO.GetComponent<RectTransform>();
            svRT.anchorMin = Vector2.zero; svRT.anchorMax = Vector2.one;
            svRT.offsetMin = new Vector2(0, CMD_H); svRT.offsetMax = new Vector2(0, -TITLE_H);
            svGO.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f);
            _scroll = svGO.AddComponent<ScrollRect>();
            _scroll.horizontal       = false;
            _scroll.scrollSensitivity = 30f;

            var vpGO = Rect("Viewport", svGO.transform);
            var vpRT = vpGO.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
            vpGO.AddComponent<Mask>().showMaskGraphic = false;
            vpGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            _scroll.viewport = vpRT;

            var ctGO = Rect("Content", vpGO.transform);
            var ctRT = ctGO.GetComponent<RectTransform>();
            ctRT.anchorMin = new Vector2(0, 1); ctRT.anchorMax = Vector2.one;
            ctRT.pivot     = new Vector2(0, 1);
            ctRT.anchoredPosition = Vector2.zero; ctRT.sizeDelta = Vector2.zero;
            ctGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.content = ctRT;

            var logGO = Rect("LogText", ctGO.transform);
            var logRT = logGO.GetComponent<RectTransform>();
            logRT.anchorMin = Vector2.zero; logRT.anchorMax = Vector2.one;
            logRT.offsetMin = new Vector2(5, 3); logRT.offsetMax = new Vector2(-5, -3);
            _logText = logGO.AddComponent<Text>();
            _logText.font             = Arial();
            _logText.fontSize         = 11;
            _logText.color            = new Color(0.85f, 0.88f, 0.85f);
            _logText.alignment        = TextAnchor.LowerLeft;
            _logText.supportRichText  = true;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow   = VerticalWrapMode.Overflow;
            logGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Command bar ──────────────────────────────────────────────────────
            var cmdRowGO = Rect("CmdRow", panelGO.transform);
            var cmdRT    = cmdRowGO.GetComponent<RectTransform>();
            cmdRT.anchorMin = Vector2.zero; cmdRT.anchorMax = new Vector2(1, 0);
            cmdRT.offsetMin = Vector2.zero; cmdRT.offsetMax = new Vector2(0, CMD_H);
            cmdRowGO.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.14f);

            var ifGO = Rect("IF", cmdRowGO.transform);
            var ifRT = ifGO.GetComponent<RectTransform>();
            ifRT.anchorMin = Vector2.zero; ifRT.anchorMax = new Vector2(1, 1);
            ifRT.offsetMin = new Vector2(3, 3); ifRT.offsetMax = new Vector2(-56, -3);
            ifGO.AddComponent<Image>().color = new Color(0.11f, 0.12f, 0.17f);
            _cmdInput = ifGO.AddComponent<InputField>();

            var phGO = Rect("PH", ifGO.transform);
            phGO.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            phGO.GetComponent<RectTransform>().anchorMax = Vector2.one;
            phGO.GetComponent<RectTransform>().offsetMin = new Vector2(5, 0);
            phGO.GetComponent<RectTransform>().offsetMax = new Vector2(-3, 0);
            var phT = phGO.AddComponent<Text>();
            phT.text = "/command or message…"; phT.font = Arial(); phT.fontSize = 11;
            phT.fontStyle = FontStyle.Italic;
            phT.color = new Color(0.35f, 0.35f, 0.40f); phT.alignment = TextAnchor.MiddleLeft;
            _cmdInput.placeholder = phT;

            var txGO = Rect("Text", ifGO.transform);
            txGO.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            txGO.GetComponent<RectTransform>().anchorMax = Vector2.one;
            txGO.GetComponent<RectTransform>().offsetMin = new Vector2(5, 0);
            txGO.GetComponent<RectTransform>().offsetMax = new Vector2(-3, 0);
            var txT = txGO.AddComponent<Text>();
            txT.text = ""; txT.font = Arial(); txT.fontSize = 11;
            txT.color = Color.white; txT.alignment = TextAnchor.MiddleLeft;
            _cmdInput.textComponent = txT;

            // Send button
            var sbGO = Rect("SendBtn", cmdRowGO.transform);
            var sbRT = sbGO.GetComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(1, 0); sbRT.anchorMax = new Vector2(1, 1);
            sbRT.offsetMin = new Vector2(-52, 3); sbRT.offsetMax = new Vector2(-3, -3);
            sbGO.AddComponent<Image>().color = new Color(0.18f, 0.35f, 0.20f);
            sbGO.AddComponent<Button>().onClick.AddListener(RunCommand);
            var sbT = Rect("L", sbGO.transform).AddComponent<Text>();
            sbT.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            sbT.GetComponent<RectTransform>().anchorMax = Vector2.one;
            sbT.GetComponent<RectTransform>().offsetMin = sbT.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            sbT.text = "Run"; sbT.font = Arial(); sbT.fontSize = 11;
            sbT.fontStyle = FontStyle.Bold; sbT.color = Color.white;
            sbT.alignment = TextAnchor.MiddleCenter;

            LayerAll(cvGO, LayerMask.NameToLayer("UI"));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Internal helpers
        // ════════════════════════════════════════════════════════════════════════

        private void LogInternal(string entry, bool redraw)
        {
            _lines.Add(entry);
            if (_lines.Count > MAX_LINES) _lines.RemoveAt(0);
            if (redraw) Redraw();
        }

        private void Redraw()
        {
            if (_logText == null) return;
            _logText.text = string.Join("\n", _lines);
            Canvas.ForceUpdateCanvases();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
        }

        private void ToggleMinimize()
        {
            _minimized = !_minimized;
            float h = _minimized ? H_MINI : H_FULL + TITLE_H + CMD_H;
            if (_panelRect) _panelRect.sizeDelta = new Vector2(W, h);
            if (_minBtnLabel) _minBtnLabel.text = _minimized ? "+" : "—";
        }

        private void UpdateBadge()
        {
            if (_statusBadge == null) return;
            var mgr = MultiplayerServerManager.Instance;
            if (mgr == null || !mgr.IsConnected)
            { _statusBadge.text = "● offline"; _statusBadge.color = new Color(0.5f, 0.5f, 0.5f); }
            else if (mgr.IsHost)
            { _statusBadge.text = $"● host ({mgr.ClientCount} clients)"; _statusBadge.color = new Color(0.3f, 0.9f, 0.3f); }
            else
            { _statusBadge.text = "● connected"; _statusBadge.color = new Color(0.3f, 0.7f, 1f); }
        }

        private void AddDrag(GameObject target)
        {
            var et = target.AddComponent<EventTrigger>();

            var begin = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            begin.callback.AddListener(data =>
            {
                Vector2 pos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panelRect.parent as RectTransform,
                    ((PointerEventData)data).position, null, out pos);
                _dragOff  = _panelRect.anchoredPosition - pos;
                _dragging = true;
            });
            et.triggers.Add(begin);

            var end = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            end.callback.AddListener(_ => _dragging = false);
            et.triggers.Add(end);
        }

        private GameObject MakeTitleBtn(string label, Transform parent, Vector2 pos,
            Color col, UnityEngine.Events.UnityAction onClick)
        {
            var go = Rect(label, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot     = new Vector2(1, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(24, 18);
            go.AddComponent<Image>().color = col;
            go.AddComponent<Button>().onClick.AddListener(onClick);
            var lbl = Rect("L", go.transform).AddComponent<Text>();
            lbl.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            lbl.GetComponent<RectTransform>().anchorMax = Vector2.one;
            lbl.GetComponent<RectTransform>().offsetMin = lbl.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            lbl.text = label; lbl.font = Arial(); lbl.fontSize = 12;
            lbl.color = Color.white; lbl.alignment = TextAnchor.MiddleCenter;
            return go;
        }

        private static GameObject Rect(string name, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            if (g.GetComponent<RectTransform>() == null)
                g.AddComponent<RectTransform>();
            return g;
        }

        private static Font Arial() => Resources.GetBuiltinResource<Font>("Arial.ttf");
        private static string Stamp(string m) => $"<color=#445>[{System.DateTime.Now:HH:mm:ss}]</color> {m}";

        private static void LayerAll(GameObject g, int l)
        {
            g.layer = l;
            foreach (Transform c in g.transform) LayerAll(c.gameObject, l);
        }
    }
}



