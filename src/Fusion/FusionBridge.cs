using System.Collections;
using System.Reflection;
using BonelabAIAgent.Infrastructure;

namespace BonelabAIAgent.Fusion;

public sealed class FusionBridge
{
    private Assembly? _assembly;
    private Type? _networkInfo;
    private Type? _playerManager;

    public bool IsAvailable => _assembly != null;

    public void Initialize()
    {
        _assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "LabFusion");
        if (_assembly == null) { AgentLog.Info("Fusion not installed; offline mode enabled."); return; }
        _networkInfo = _assembly.GetType("LabFusion.Network.NetworkInfo");
        _playerManager = _assembly.GetType("LabFusion.Player.PlayerIDManager");
        AgentLog.Info($"Fusion bridge loaded for {_assembly.GetName().Version}.");
    }

    public object GetSession()
    {
        if (_networkInfo == null) return new { available = false, online = false, mode = "offline", synchronization = "not applicable" };
        var online = ReadStatic<bool>(_networkInfo, "HasServer");
        var host = ReadStatic<bool>(_networkInfo, "IsHost");
        var client = ReadStatic<bool>(_networkInfo, "IsClient");
        var platform = ReadStatic<string>(_networkInfo, "Platform") ?? "None";
        return new { available = true, online, isHost = host, isClient = client, mode = host ? "host" : client ? "client" : "offline", platform };
    }

    public object GetPlayers()
    {
        if (_playerManager == null) return Array.Empty<object>();
        var value = _playerManager.GetField("PlayerIDs", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as IEnumerable;
        if (value == null) return Array.Empty<object>();
        var output = new List<object>();
        foreach (var player in value)
        {
            if (player == null) continue;
            var type = player.GetType();
            output.Add(new
            {
                smallId = Read<byte>(player, type, "SmallID"),
                platformId = Read<ulong>(player, type, "PlatformID").ToString(),
                isHost = Read<bool>(player, type, "IsHost"),
                isMe = Read<bool>(player, type, "IsMe")
            });
        }
        return output;
    }

    public object GetSynchronizationReport() => new
    {
        ordinaryFusion = new[] { "spawn/despawn", "prop ownership and transforms", "grabs/releases", "gun shots", "NPC death", "seats", "avatar changes" },
        localOnly = new[] { "health overrides", "strength/speed/vitality overrides", "Codex conversation and UI" },
        customProtocol = "capability negotiation is provided by the optional BonelabAIAgent.Fusion module"
    };

    private static T? ReadStatic<T>(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        return property?.GetValue(null) is T value ? value : default;
    }

    private static T Read<T>(object target, Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetValue(target) is T pv) return pv;
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(target) is T fv ? fv : default!;
    }
}
