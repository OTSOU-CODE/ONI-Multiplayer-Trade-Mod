using System;
using UnityEngine;

namespace ONI_MP.Misc
{
    public static class Utils
    {
        public static string ColorText(string text, string color)
        {
            return $"<color=#{color}>{text}</color>";
        }

        public static string TrucateName(string name, int length = 20)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Length <= length ? name : name.Substring(0, length) + "...";
        }

        public static bool IsInGame()
        {
            return Game.Instance != null;
        }
    }
}
