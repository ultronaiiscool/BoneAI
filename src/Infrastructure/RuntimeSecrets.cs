namespace BoneAI.Infrastructure;

/// <summary>Secrets entered in-game live only for the current BONELAB process.</summary>
public static class RuntimeSecrets
{
    private static readonly object Gate = new();
    private static string? _codexTransportToken;
    private static readonly Dictionary<string, string> ProviderApiKeys = new(StringComparer.OrdinalIgnoreCase);

    public static string? CodexTransportToken
    {
        get { lock (Gate) return _codexTransportToken; }
        set { lock (Gate) _codexTransportToken = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    }

    public static void SetProviderApiKey(string provider, string? value)
    {
        lock (Gate)
        {
            if (string.IsNullOrWhiteSpace(value)) ProviderApiKeys.Remove(provider);
            else ProviderApiKeys[provider] = value.Trim();
        }
    }

    public static string? GetProviderApiKey(string provider)
    {
        lock (Gate) return ProviderApiKeys.TryGetValue(provider, out var value) ? value : null;
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _codexTransportToken = null;
            ProviderApiKeys.Clear();
        }
    }
}
