using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerTradeMod
{
    /// <summary>
    /// Patches ClusterMapScreen to inject the remote player's colony as a
    /// native-looking planetoid cell inside the actual cluster map grid.
    ///
    /// Strategy: find the cluster map's world-entry container, clone one of
    /// the existing ClusterMapWorldEntry GameObjects, strip its game-world
    /// logic, and populate it with remote colony data.  The cloned cell is
    /// positioned adjacent to the last world in the grid so it fits naturally.
    /// </summary>
    [HarmonyPatch(typeof(ClusterMapScreen), "OnShow")]
    public static class ClusterMapPatch
    {
        // Injected GO that lives on the ClusterMapScreen canvas
        private static GameObject _remoteCell;

        public static void Postfix(ClusterMapScreen __instance)
        {
            // Subscribe/re-subscribe every time the screen opens
            if (RemoteColonyManager.Instance != null)
            {
                RemoteColonyManager.Instance.OnRemoteColonyUpdated -= OnColonyUpdated;
                RemoteColonyManager.Instance.OnRemoteColonyUpdated += OnColonyUpdated;
            }

            RefreshRemoteCell(__instance);
        }

        // ── Called whenever new colony data arrives ───────────────────────────

        private static ClusterMapScreen _screen;

        private static void OnColonyUpdated(RemoteColonyInfo info)
        {
            if (_screen == null)
                _screen = UnityEngine.Object.FindObjectOfType<ClusterMapScreen>();
            if (_screen != null)
                RefreshRemoteCell(_screen);
        }

        // ── Build or Update the remote-colony cell ────────────────────────────

        private static void RefreshRemoteCell(ClusterMapScreen screen)
        {
            _screen = screen;
            var info = RemoteColonyManager.Instance?.Current;

            if (info == null)
            {
                if (_remoteCell != null) _remoteCell.SetActive(false);
                return;
            }

            if (_remoteCell == null)
                BuildCell(screen);

            if (_remoteCell == null) return;

            _remoteCell.SetActive(true);
            UpdateCell(info);
        }

        // ── Construct the cell ────────────────────────────────────────────────

        private static Text _nameLabel;
        private static Text _subtitleLabel;
        private static Text _detailsLabel;

        private static void BuildCell(ClusterMapScreen screen)
        {
            try
            {
                // Find the scrollable world-list container inside ClusterMapScreen
                Transform container = null;
                var transforms = screen.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    if (t.name.IndexOf("world", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                        t.childCount > 0)
                    {
                        container = t;
                        break;
                    }
                }
                if (container == null) container = screen.transform;

                // Clone an existing world entry if possible — use reflection to avoid
                // direct dependency on ClusterMapWorldEntry's assembly
                GameObject srcPrefab = null;
                var allCmps = container.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var mb in allCmps)
                {
                    if (mb.GetType().Name.IndexOf("WorldEntry", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        srcPrefab = mb.gameObject;
                        break;
                    }
                }

                GameObject cell;
                if (srcPrefab != null)
                {
                    cell = UnityEngine.Object.Instantiate(srcPrefab, container);
                    // Destroy the game-logic component so it doesn't try to update from game data
                    foreach (var mb in cell.GetComponents<MonoBehaviour>())
                    {
                        if (mb.GetType().Name.IndexOf("WorldEntry", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            UnityEngine.Object.Destroy(mb);
                            break;
                        }
                    }
                }
                else
                {
                    // Fallback: build a simple card from scratch
                    cell = BuildFallbackCard(container);
                }

                cell.name = "RemoteColonyEntry";
                _remoteCell = cell;

                // Mark with a special tint so players can distinguish remote vs local
                TintRemote(cell);

                // Collect text components (first = name, second = subtitle if exists)
                var texts = cell.GetComponentsInChildren<Text>(true);
                if (texts.Length >= 1) _nameLabel     = texts[0];
                if (texts.Length >= 2) _subtitleLabel = texts[1];

                // Add a third text for the resource summary
                _detailsLabel = AddDetailsText(cell.transform);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Multiplayer] ClusterMapPatch.BuildCell failed: " + ex.Message);
                _remoteCell = null;
            }
        }

        private static void UpdateCell(RemoteColonyInfo info)
        {
            if (_nameLabel != null)
                _nameLabel.text = $"🌍 {info.WorldName}";

            if (_subtitleLabel != null)
                _subtitleLabel.text = $"Cycle {info.Cycle}  ·  {info.WorldType}";

            if (_detailsLabel != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"👥 {info.DupeCount} dupes    🚀 {info.RocketCount} rockets");

                if (info.TopResources?.Count > 0)
                {
                    sb.AppendLine("─── Resources ───");
                    foreach (var r in info.TopResources)
                        sb.AppendLine($"  {Friendly(r.Tag),-18}  {FormatKg(r.Kg),8}");
                }

                if (info.Geysers?.Count > 0)
                {
                    sb.AppendLine($"─── Geysers ({info.Geysers.Count}) ───");
                    foreach (var g in info.Geysers)
                        sb.AppendLine($"  🌋 {Friendly(g.Type)}");
                }

                _detailsLabel.text = sb.ToString().TrimEnd();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// Tint all Image children to a distinctive blue to mark this as "remote"
        private static void TintRemote(GameObject cell)
        {
            foreach (var img in cell.GetComponentsInChildren<Image>(true))
                img.color = Color.Lerp(img.color, new Color(0.4f, 0.6f, 1f, img.color.a), 0.35f);
        }

        private static Text AddDetailsText(Transform parent)
        {
            var go = new GameObject("RemoteColonyDetails");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0, 160);
            rt.anchoredPosition = new Vector2(0, -2);
            var t = go.AddComponent<Text>();
            t.font             = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize         = 10;
            t.color            = new Color(0.85f, 0.9f, 1f);
            t.alignment        = TextAnchor.UpperLeft;
            t.supportRichText  = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            return t;
        }

        private static GameObject BuildFallbackCard(Transform parent)
        {
            var cell = new GameObject("RemoteColonyEntry");
            cell.transform.SetParent(parent, false);
            var rt = cell.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180, 240);

            var bg = cell.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.22f, 0.95f);

            var outline = cell.AddComponent<Outline>();
            outline.effectColor    = new Color(0.4f, 0.6f, 1f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Name label
            var nameGo = new GameObject("Name"); nameGo.transform.SetParent(cell.transform, false);
            var nameRT = nameGo.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0,1); nameRT.anchorMax = new Vector2(1,1);
            nameRT.offsetMin = new Vector2(8,-36); nameRT.offsetMax = new Vector2(-8,0);
            _nameLabel = nameGo.AddComponent<Text>();
            _nameLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _nameLabel.fontSize = 13; _nameLabel.fontStyle = FontStyle.Bold;
            _nameLabel.color = new Color(0.55f, 0.8f, 1f);
            _nameLabel.alignment = TextAnchor.MiddleCenter;

            // Subtitle label
            var subGo = new GameObject("Subtitle"); subGo.transform.SetParent(cell.transform, false);
            var subRT = subGo.AddComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0,1); subRT.anchorMax = new Vector2(1,1);
            subRT.offsetMin = new Vector2(8,-56); subRT.offsetMax = new Vector2(-8,-36);
            _subtitleLabel = subGo.AddComponent<Text>();
            _subtitleLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _subtitleLabel.fontSize = 10; _subtitleLabel.color = new Color(0.7f,0.8f,0.7f);
            _subtitleLabel.alignment = TextAnchor.MiddleCenter;

            return cell;
        }

        private static string FormatKg(float kg)
        {
            if (kg >= 1_000_000f) return $"{kg/1_000_000f:F1}t";
            if (kg >= 1_000f)     return $"{kg/1_000f:F1}kg";
            return $"{kg:F0}g";
        }

        private static string Friendly(string s)
        {
            if (string.IsNullOrEmpty(s)) return "?";
            s = s.Replace("_", " ").Trim();
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
