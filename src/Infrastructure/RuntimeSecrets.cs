using System.Text.RegularExpressions;

namespace BoneAI.Infrastructure;

/// <summary>In-memory secret cache backed by platform-protected storage.</summary>
public static class RuntimeSecrets
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, string> ProviderApiKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SecureSecretStore Store = new();
    public static string? LastStorageError { get; private set; }

    public static void Initialize()
    {
        lock (Gate)
        {
            ProviderApiKeys.Clear();
            try { foreach (var pair in Store.LoadAll()) ProviderApiKeys[pair.Key] = pair.Value; LastStorageError = null; }
            catch (Exception ex) { LastStorageError = ex.GetBaseException().Message; AgentLog.Warn("Secure API-key storage unavailable; new keys will be session-only: " + LastStorageError); }
        }
    }

    public static bool SetProviderApiKey(string provider, string? value)
    {
        lock (Gate)
        {
            provider = Normalize(provider);
            if (string.IsNullOrWhiteSpace(value)) ProviderApiKeys.Remove(provider); else ProviderApiKeys[provider] = value.Trim();
            try { if (string.IsNullOrWhiteSpace(value)) Store.Remove(provider); else Store.Save(provider, value.Trim()); LastStorageError = null; return true; }
            catch (Exception ex) { LastStorageError = ex.GetBaseException().Message; AgentLog.Warn("Could not update secure API-key storage: " + LastStorageError); return false; }
        }
    }

    public static string? GetProviderApiKey(string provider) { lock (Gate) return ProviderApiKeys.TryGetValue(Normalize(provider), out var value) ? value : null; }
    public static bool HasProviderApiKey(string provider) { lock (Gate) return ProviderApiKeys.ContainsKey(Normalize(provider)); }

    public static string Redact(string? message)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;
        var result = message;
        lock (Gate) foreach (var secret in ProviderApiKeys.Values.Where(x => x.Length >= 6)) result = result.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        result = Regex.Replace(result, @"(?i)(authorization\s*[:=]\s*bearer\s+)[^\s,;]+", "$1[REDACTED]");
        result = Regex.Replace(result, @"(?i)((?:api[-_ ]?key|x-api-key)\s*[:=]\s*)[^\s,;]+", "$1[REDACTED]");
        return Regex.Replace(result, @"\b(?:sk|xai|sk-or)-[A-Za-z0-9_\-]{12,}\b", "[REDACTED]");
    }

    public static void Clear()
    {
        lock (Gate) { ProviderApiKeys.Clear(); try { Store.Clear(); LastStorageError = null; } catch (Exception ex) { LastStorageError = ex.GetBaseException().Message; } }
    }

    private static string Normalize(string provider) => (provider ?? string.Empty).Trim().ToLowerInvariant();
}
