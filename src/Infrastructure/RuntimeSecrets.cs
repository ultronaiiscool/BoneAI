namespace BoneAI.Infrastructure;

/// <summary>Secrets entered in-game live only for the current BONELAB process.</summary>
public static class RuntimeSecrets
{
    private static readonly object Gate = new();
    private static string? _codexTransportToken;

    public static string? CodexTransportToken
    {
        get { lock (Gate) return _codexTransportToken; }
        set { lock (Gate) _codexTransportToken = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    }

    public static void Clear()
    {
        lock (Gate) _codexTransportToken = null;
    }
}
