namespace ONI_MP.DebugTools
{
    public static class DebugConsole
    {
        public static void Init() {}
        public static void Log(object message) => UnityEngine.Debug.Log("[ONI_MP] " + message);
        public static void LogError(object message) => UnityEngine.Debug.LogError("[ONI_MP Error] " + message);
        public static void LogWarning(object message) => UnityEngine.Debug.LogWarning("[ONI_MP Warn] " + message);
        public static void LogSuccess(object message) => UnityEngine.Debug.Log("[ONI_MP Success] " + message);
        public static void LogAssert(object message) => UnityEngine.Debug.Log("[ONI_MP Assert] " + message);
        public static void LogException(System.Exception e) => UnityEngine.Debug.LogException(e);
    }
}
