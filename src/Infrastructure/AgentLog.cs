using MelonLoader;

namespace BoneAI.Infrastructure;

public static class AgentLog
{
    public static bool Verbose { get; set; }
    // MelonLoader already prefixes every message with this mod's BoneAI name.
    public static void Info(string message) => MelonLogger.Msg(message);
    public static void Warn(string message) => MelonLogger.Warning(message);
    public static void Error(string message) => MelonLogger.Error(message);
    public static void Debug(string message) { if (Verbose) MelonLogger.Msg($"[Debug] {message}"); }
    public static void Exception(string scope, Exception ex) => MelonLogger.Error($"{scope}: {ex}");
}
