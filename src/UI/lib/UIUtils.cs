using UnityEngine;

namespace ONI_MP.UI.lib
{
    public static class UIUtils
    {
        public static Color rgb(float r, float g, float b)
        {
            return new Color(r / 255f, g / 255f, b / 255f);
        }

        public static Color rgba(float r, float g, float b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        public static Color Lighten(Color color, float percent)
        {
            return new Color(
                Mathf.Clamp01(color.r + (percent / 100f)),
                Mathf.Clamp01(color.g + (percent / 100f)),
                Mathf.Clamp01(color.b + (percent / 100f)),
                color.a
            );
        }

        public static void AddSimpleTooltipToObject(Transform parent, string text, bool alignCenter = false, float wrapWidth = 0f)
        {
            AddSimpleTooltipToObject(parent.gameObject, text, alignCenter, wrapWidth);
        }

        public static void AddSimpleTooltipToObject(GameObject parent, string text, bool alignCenter = false, float wrapWidth = 0f)
        {
            var tooltip = parent.GetComponent<ToolTip>() ?? parent.AddComponent<ToolTip>();
            tooltip.toolTip = text;
            tooltip.ClearMultiStringTooltip();
            tooltip.AddMultiStringTooltip(text, null);
        }
    }
}
