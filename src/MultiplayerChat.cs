using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace MultiplayerTradeMod
{
    /// <summary>
    /// Persistent in-game chat overlay — bottom-right, Ctrl+T to toggle.
    /// Improved: proper layout, no RectTransform double-add, auto-scroll, timestamps.
    /// </summary>
    public class MultiplayerChat : MonoBehaviour
    {
        public static MultiplayerChat Instance { get; private set; }

        // Layout constants
        private const float WIN_W    = 380f;
        private const float BODY_H   = 220f;
        private const float HEADER_H = 28f;
        private const float INPUT_H  = 34f;
        private const float TOTAL_H  = HEADER_H + BODY_H + INPUT_H;

        // State
        private bool _visible    = true;
        private bool _minimized  = false;
        private bool _dragging   = false;
        private Vector2 _dragOff;

        // UI refs
        private RectTransform _winRect;
        private Transform     _msgContainer;
        private ScrollRect    _scroll;
        private InputField    _inputField;
        private Text          _connStatus;
        private Text          _minBtnLabel;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private const int MAX_ROWS = 80;

        // Player nickname (editable, persisted)
        private const string PREFS_NAME_KEY = "MP_PlayerName";
        private string _localName;
        private InputField _nameField;

        // ── Unity lifecycle ─────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Load or default nickname
            _localName = PlayerPrefs.GetString(PREFS_NAME_KEY,
                System.Environment.MachineName.Replace(" ", "_"));
            BuildUI();
            AddSystemMsg("Chat ready — Ctrl+T to show/hide, Enter to send.");
        }

        private void Update()
        {
            // Toggle visibility
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.T))
                SetVisible(!_visible);

            // Send on Enter (when input focused)
            if (_visible && !_minimized && _inputField != null && _inputField.isFocused)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    TrySendMessage();
            }

            // Drag
            if (_dragging && _winRect != null)
            {
                Vector2 mp;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _winRect.parent as RectTransform, Input.mousePosition, null, out mp);
                _winRect.anchoredPosition = mp + _dragOff;
            }
        }

        private float _statusTick = 0f;
        private void LateUpdate()
        {
            // Refresh connection badge ~1/sec
            _statusTick -= Time.unscaledDeltaTime;
            if (_statusTick > 0f || _connStatus == null) return;
            _statusTick = 1f;

            var mgr = MultiplayerServerManager.Instance;
            if (mgr == null || !mgr.IsConnected)
            { _connStatus.text = "● disconnected"; _connStatus.color = new Color(0.5f, 0.5f, 0.5f); }
            else if (mgr.IsHost)
            { _connStatus.text = "● hosting";      _connStatus.color = new Color(0.3f, 0.9f, 0.3f); }
            else
            { _connStatus.text = "● connected";    _connStatus.color = new Color(0.3f, 0.7f, 1f);   }
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>Called by NetworkManager when a CHAT packet arrives.</summary>
        public void ReceiveMessage(string sender, string text)
        {
            string ts = System.DateTime.Now.ToString("HH:mm");
            AddRow(ts, sender, text, new Color(0.45f, 0.8f, 1f), new Color(0.9f, 0.9f, 0.9f));
        }

        // ── UI construction ─────────────────────────────────────────────────────

        private void BuildUI()
        {
            // ── Canvas ──────────────────────────────────────────────────────────
            var cvGO = NGO("ChatCanvas", transform);
            var cv   = cvGO.AddComponent<Canvas>();
            cv.renderMode  = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 4500;
            cvGO.AddComponent<CanvasScaler>();
            cvGO.AddComponent<GraphicRaycaster>();

            // ── Window (bottom-right) ────────────────────────────────────────────
            var win = NGO("ChatWindow", cvGO.transform);
            _winRect = win.AddComponent<RectTransform>();
            _winRect.anchorMin = _winRect.anchorMax = new Vector2(1f, 0f);
            _winRect.pivot     = new Vector2(1f, 0f);
            _winRect.anchoredPosition = new Vector2(-8f, 8f);
            _winRect.sizeDelta = new Vector2(WIN_W, TOTAL_H);
            win.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.93f);

            // ── Header bar ──────────────────────────────────────────────────────
            var hdr    = NGO("Header", win.transform);
            var hdrRT  = hdr.AddComponent<RectTransform>();
            hdrRT.anchorMin = new Vector2(0, 1); hdrRT.anchorMax = Vector2.one;
            hdrRT.offsetMin = Vector2.zero;       hdrRT.offsetMax = new Vector2(0, -HEADER_H);
            hdr.AddComponent<Image>().color = new Color(0.11f, 0.13f, 0.21f);

            // Title
            var titleGO = NGO("Title", hdr.transform);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
            titleRT.offsetMin = new Vector2(8, 0); titleRT.offsetMax = new Vector2(-90, 0);
            var titleT = titleGO.AddComponent<Text>();
            titleT.text      = "💬  CHAT";
            titleT.font      = Arial();
            titleT.fontSize  = 12;
            titleT.fontStyle = FontStyle.Bold;
            titleT.color     = new Color(0.8f, 0.85f, 1f);
            titleT.alignment = TextAnchor.MiddleLeft;

            // Drag behavior on header
            var et = hdr.AddComponent<EventTrigger>();
            var pd = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pd.callback.AddListener((data) => {
                _dragging = true;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _winRect.parent as RectTransform, ((PointerEventData)data).position, null, out Vector2 local);
                _dragOff = _winRect.anchoredPosition - local;
            });
            et.triggers.Add(pd);

            var pu = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            pu.callback.AddListener((data) => _dragging = false);
            et.triggers.Add(pu);

            // Connection status badge
            var statGO = NGO("ConnStat", hdr.transform);
            var statRT = statGO.AddComponent<RectTransform>();
            statRT.anchorMin = Vector2.zero; statRT.anchorMax = Vector2.one;
            statRT.offsetMin = new Vector2(0, 0); statRT.offsetMax = new Vector2(-38, 0);
            _connStatus = statGO.AddComponent<Text>();
            _connStatus.font      = Arial();
            _connStatus.fontSize  = 10;
            _connStatus.color     = new Color(0.5f, 0.5f, 0.5f);
            _connStatus.alignment = TextAnchor.MiddleRight;

            // Minimize button
            var minBtnGO = NGO("MinBtn", hdr.transform);
            var minRT    = minBtnGO.AddComponent<RectTransform>();
            minRT.anchorMin = minRT.anchorMax = new Vector2(1f, 0.5f);
            minRT.pivot     = new Vector2(1f, 0.5f);
            minRT.anchoredPosition = new Vector2(-3f, 0f);
            minRT.sizeDelta = new Vector2(30f, 20f);
            minBtnGO.AddComponent<Image>().color = new Color(0.25f, 0.26f, 0.38f);
            var minBtn = minBtnGO.AddComponent<Button>();

            var minLblGO = NGO("Lbl", minBtnGO.transform);
            var minLblRT = minLblGO.AddComponent<RectTransform>();
            minLblRT.anchorMin = Vector2.zero; minLblRT.anchorMax = Vector2.one;
            minLblRT.offsetMin = minLblRT.offsetMax = Vector2.zero;
            _minBtnLabel = minLblGO.AddComponent<Text>();
            _minBtnLabel.text      = "—";
            _minBtnLabel.font      = Arial();
            _minBtnLabel.fontSize  = 14;
            _minBtnLabel.color     = Color.white;
            _minBtnLabel.alignment = TextAnchor.MiddleCenter;
            minBtn.onClick.AddListener(ToggleMinimize);

            // ── Message scroll area ─────────────────────────────────────────────
            var svGO  = NGO("Scroll", win.transform);
            var svRT  = svGO.AddComponent<RectTransform>();
            svRT.anchorMin = Vector2.zero;        svRT.anchorMax = Vector2.one;
            svRT.offsetMin = new Vector2(0, INPUT_H + 26); svRT.offsetMax = new Vector2(0, -HEADER_H);
            svGO.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f);
            _scroll = svGO.AddComponent<ScrollRect>();
            _scroll.horizontal       = false;
            _scroll.scrollSensitivity = 25f;

            var vpGO = NGO("Viewport", svGO.transform);
            var vpRT = vpGO.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
            vpGO.AddComponent<Mask>().showMaskGraphic = false;
            vpGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            _scroll.viewport = vpRT;

            var ctGO = NGO("Content", vpGO.transform);
            var ctRT = ctGO.AddComponent<RectTransform>();
            ctRT.anchorMin = new Vector2(0, 0); ctRT.anchorMax = new Vector2(1, 0);
            ctRT.pivot     = new Vector2(0, 0);
            ctRT.anchoredPosition = Vector2.zero;
            ctRT.sizeDelta = Vector2.zero;
            var vl = ctGO.AddComponent<VerticalLayoutGroup>();
            vl.spacing           = 1;
            vl.padding           = new RectOffset(4, 4, 3, 3);
            vl.childControlHeight  = true;
            vl.childControlWidth   = true;
            vl.childForceExpandWidth  = true;
            vl.childForceExpandHeight = false;
            ctGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.content   = ctRT;
            _msgContainer = ctGO.transform;

            // ── Nickname row ───────────────────────────────────────────────────
            var nameRowGO = NGO("NameRow", win.transform);
            var nameRowRT = nameRowGO.AddComponent<RectTransform>();
            nameRowRT.anchorMin = Vector2.zero; nameRowRT.anchorMax = new Vector2(1, 0);
            nameRowRT.offsetMin = new Vector2(0, INPUT_H); nameRowRT.offsetMax = new Vector2(0, INPUT_H + 26);
            nameRowGO.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f);

            var nLblGO = NGO("NLbl", nameRowGO.transform);
            var nLblRT = nLblGO.AddComponent<RectTransform>();
            nLblRT.anchorMin = Vector2.zero; nLblRT.anchorMax = new Vector2(0, 1);
            nLblRT.offsetMin = new Vector2(6, 0); nLblRT.offsetMax = new Vector2(56, 0);
            var nLblT = nLblGO.AddComponent<Text>();
            nLblT.text = "Name:"; nLblT.font = Arial(); nLblT.fontSize = 10;
            nLblT.color = new Color(0.5f, 0.5f, 0.55f); nLblT.alignment = TextAnchor.MiddleLeft;

            var nfGO = NGO("NameInput", nameRowGO.transform);
            var nfRT = nfGO.AddComponent<RectTransform>();
            nfRT.anchorMin = new Vector2(0, 0); nfRT.anchorMax = new Vector2(1, 1);
            nfRT.offsetMin = new Vector2(58, 2); nfRT.offsetMax = new Vector2(-4, -2);
            nfGO.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f);
            _nameField = nfGO.AddComponent<InputField>();
            var nfPhGO = NGO("PH", nfGO.transform);
            var nfPhRT = nfPhGO.AddComponent<RectTransform>();
            nfPhRT.anchorMin = Vector2.zero; nfPhRT.anchorMax = Vector2.one;
            nfPhRT.offsetMin = new Vector2(6, 0); nfPhRT.offsetMax = new Vector2(-4, 0);
            var nfPh = nfPhGO.AddComponent<Text>();
            nfPh.text = "Your nickname"; nfPh.font = Arial(); nfPh.fontSize = 10;
            nfPh.color = new Color(0.35f, 0.35f, 0.40f); nfPh.alignment = TextAnchor.MiddleLeft;
            _nameField.placeholder = nfPh;
            var nfTxtGO = NGO("Text", nfGO.transform);
            var nfTxtRT = nfTxtGO.AddComponent<RectTransform>();
            nfTxtRT.anchorMin = Vector2.zero; nfTxtRT.anchorMax = Vector2.one;
            nfTxtRT.offsetMin = new Vector2(6, 0); nfTxtRT.offsetMax = new Vector2(-4, 0);
            var nfTxt = nfTxtGO.AddComponent<Text>();
            nfTxt.text = _localName; nfTxt.font = Arial(); nfTxt.fontSize = 11;
            nfTxt.color = new Color(0.9f, 0.85f, 0.55f); nfTxt.alignment = TextAnchor.MiddleLeft;
            _nameField.textComponent = nfTxt;
            _nameField.text = _localName;
            _nameField.onValueChanged.AddListener((v) => {
                string safe = (v ?? "").Trim();
                _localName = string.IsNullOrEmpty(safe) ? System.Environment.MachineName : safe;
                PlayerPrefs.SetString(PREFS_NAME_KEY, _localName);
            });

            // ── Input row ───────────────────────────────────────────────────────
            var inRowGO = NGO("InputRow", win.transform);
            var inRowRT = inRowGO.AddComponent<RectTransform>();
            inRowRT.anchorMin = Vector2.zero;      inRowRT.anchorMax = new Vector2(1, 0);
            inRowRT.offsetMin = Vector2.zero;      inRowRT.offsetMax = new Vector2(0, INPUT_H);
            inRowGO.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.13f);


            // Text field
            var ifGO = NGO("InputField", inRowGO.transform);
            var ifRT = ifGO.AddComponent<RectTransform>();
            ifRT.anchorMin = Vector2.zero;           ifRT.anchorMax = new Vector2(1, 1);
            ifRT.offsetMin = new Vector2(4, 3);      ifRT.offsetMax = new Vector2(-58, -3);
            ifGO.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.20f);
            _inputField = ifGO.AddComponent<InputField>();

            var phGO = NGO("PH", ifGO.transform);
            var phRT = phGO.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(6, 0); phRT.offsetMax = new Vector2(-4, 0);
            var phT = phGO.AddComponent<Text>();
            phT.text      = "Type a message…";
            phT.font      = Arial();
            phT.fontSize  = 11;
            phT.fontStyle = FontStyle.Italic;
            phT.color     = new Color(0.38f, 0.38f, 0.42f);
            phT.alignment = TextAnchor.MiddleLeft;
            _inputField.placeholder = phT;

            var txtGO = NGO("Text", ifGO.transform);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(6, 0); txtRT.offsetMax = new Vector2(-4, 0);
            var txtT = txtGO.AddComponent<Text>();
            txtT.text      = "";
            txtT.font      = Arial();
            txtT.fontSize  = 12;
            txtT.color     = Color.white;
            txtT.alignment = TextAnchor.MiddleLeft;
            _inputField.textComponent = txtT;

            // Send button
            var sbGO = NGO("SendBtn", inRowGO.transform);
            var sbRT = sbGO.AddComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(1, 0); sbRT.anchorMax = new Vector2(1, 1);
            sbRT.offsetMin = new Vector2(-54, 3); sbRT.offsetMax = new Vector2(-3, -3);
            sbGO.AddComponent<Image>().color = new Color(0.15f, 0.35f, 0.55f);
            sbGO.AddComponent<Button>().onClick.AddListener(TrySendMessage);
            var sbLbl = NGO("L", sbGO.transform);
            var sbLblRT = sbLbl.AddComponent<RectTransform>();
            sbLblRT.anchorMin = Vector2.zero; sbLblRT.anchorMax = Vector2.one;
            sbLblRT.offsetMin = sbLblRT.offsetMax = Vector2.zero;
            var sbT = sbLbl.AddComponent<Text>();
            sbT.text      = "Send";
            sbT.font      = Arial();
            sbT.fontSize  = 11;
            sbT.fontStyle = FontStyle.Bold;
            sbT.color     = Color.white;
            sbT.alignment = TextAnchor.MiddleCenter;

            ApplyLayerRecursive(cvGO, LayerMask.NameToLayer("UI"));
        }

        // ── Message helpers ─────────────────────────────────────────────────────

        private void AddSystemMsg(string text)
        {
            string ts = System.DateTime.Now.ToString("HH:mm");
            AddRow(ts, "System", text, new Color(0.5f, 0.8f, 1f), new Color(0.65f, 0.65f, 0.65f));
        }

        private void AddRow(string timestamp, string sender, string text, Color senderColor, Color textColor)
        {
            // Prune old messages
            while (_rows.Count >= MAX_ROWS)
            {
                if (_rows[0] != null) Destroy(_rows[0]);
                _rows.RemoveAt(0);
            }

            // Container row
            var rowGO = NGO("Row", _msgContainer);
            if (rowGO.GetComponent<RectTransform>() == null) rowGO.AddComponent<RectTransform>();               // just so layout works
            var csf = rowGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth  = false;
            hlg.childControlHeight     = true;
            hlg.childControlWidth      = false;
            hlg.spacing = 3;
            hlg.padding = new RectOffset(2, 2, 1, 1);

            // Timestamp
            MakeLabel(rowGO.transform, $"[{timestamp}]", 52f, Arial(), 10,
                new Color(0.4f, 0.4f, 0.45f), FontStyle.Normal, TextAnchor.UpperLeft);

            // Sender
            MakeLabel(rowGO.transform, sender + ":", 76f, Arial(), 11,
                senderColor, FontStyle.Bold, TextAnchor.UpperLeft);

            // Message (flexible)
            MakeLabelFlex(rowGO.transform, text, Arial(), 11, textColor, FontStyle.Normal, TextAnchor.UpperLeft);

            _rows.Add(rowGO);
            StartCoroutine(ScrollToBottom());
        }

        private IEnumerator ScrollToBottom()
        {
            yield return null; // wait one frame for layout rebuild
            Canvas.ForceUpdateCanvases();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
        }

        // ── Send logic ──────────────────────────────────────────────────────────

        private void TrySendMessage()
        {
            if (_inputField == null) return;
            string text = (_inputField.text ?? "").Trim();
            if (text.Length == 0) return;

            _inputField.text = "";

            // Show locally (gold for "me")
            string ts = System.DateTime.Now.ToString("HH:mm");
            AddRow(ts, "Me", text, new Color(0.9f, 0.75f, 0.25f), new Color(0.9f, 0.9f, 0.9f));

            MultiplayerServerManager.Instance?.SendChat(_localName, text);

            _inputField.ActivateInputField();
        }

        // ── Minimize ────────────────────────────────────────────────────────────

        private void ToggleMinimize()
        {
            _minimized = !_minimized;
            if (_winRect != null)
                _winRect.sizeDelta = _minimized
                    ? new Vector2(WIN_W, HEADER_H)
                    : new Vector2(WIN_W, TOTAL_H);
            if (_minBtnLabel != null)
                _minBtnLabel.text = _minimized ? "+" : "—";
        }

        private void SetVisible(bool v)
        {
            _visible = v;
            if (_winRect != null) _winRect.gameObject.SetActive(_visible);
        }

        // ── Static helpers ──────────────────────────────────────────────────────

        private static GameObject NGO(string name, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g;
        }

        private static Font Arial() => Resources.GetBuiltinResource<Font>("Arial.ttf");

        private static void MakeLabel(Transform parent, string text, float fixedWidth,
            Font font, int size, Color color, FontStyle style, TextAnchor align)
        {
            var go  = NGO("Lbl", parent);
            var rt  = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            var le  = go.AddComponent<LayoutElement>();
            le.minWidth      = fixedWidth;
            le.preferredWidth = fixedWidth;
            le.flexibleWidth = 0;
            var t   = go.AddComponent<Text>();
            t.text      = text;
            t.font      = font;
            t.fontSize  = size;
            t.fontStyle = style;
            t.color     = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
        }

        private static void MakeLabelFlex(Transform parent, string text,
            Font font, int size, Color color, FontStyle style, TextAnchor align)
        {
            var go = NGO("Msg", parent);
            if (go.GetComponent<RectTransform>() == null) go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            var t  = go.AddComponent<Text>();
            t.text      = text;
            t.font      = font;
            t.fontSize  = size;
            t.fontStyle = style;
            t.color     = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void ApplyLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform)
                ApplyLayerRecursive(c.gameObject, layer);
        }
    }
}

