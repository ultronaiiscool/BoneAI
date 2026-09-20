using MelonLoader;

namespace BonelabAIAgent.Infrastructure;

public static class AgentLog
{
    public static bool Verbose { get; set; }
    public static void Info(string message) => MelonLogger.Msg($"[AIAgent] {message}");
    public static void Warn(string message) => MelonLogger.Warning($"[AIAgent] {message}");
    public static void Error(string message) => MelonLogger.Error($"[AIAgent] {message}");
    public static void Debug(string message) { if (Verbose) MelonLogger.Msg($"[AIAgent:Debug] {message}"); }
    public static void Exception(string scope, Exception ex) => MelonLogger.Error($"[AIAgent] {scope}: {ex}");
}
