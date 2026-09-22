using MelonLoader;

namespace BoneAI.Infrastructure;

public static class AgentLog
{
    public static bool Verbose { get; set; }
    // MelonLoader already prefixes every message with this mod's BoneAI name.
    public static void Info(string message) => MelonLogger.Msg(RuntimeSecrets.Redact(message));
    public static void Warn(string message) => MelonLogger.Warning(RuntimeSecrets.Redact(message));
    public static void Error(string message) => MelonLogger.Error(RuntimeSecrets.Redact(message));
    public static void Debug(string message) { if (Verbose) MelonLogger.Msg(RuntimeSecrets.Redact($"[Debug] {message}")); }
    public static void Exception(string scope, Exception ex) => MelonLogger.Error(RuntimeSecrets.Redact($"{scope}: {ex}"));
}
