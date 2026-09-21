using System.Collections;
using System.Reflection;
using BonelabAIAgent.Infrastructure;
using Il2CppSLZ.Marrow.Combat;
using Il2CppSLZ.Marrow.Data;
using UnityEngine;

namespace BonelabAIAgent.Fusion;

public sealed class FusionBridge
{
    public sealed record PlayerSnapshot(byte SmallId, ulong PlatformId, string Username, bool IsHost, bool IsMe, GameObject? RigObject, Vector3 Position);

    private Assembly? _assembly;
    private Type? _networkInfo;
    private Type? _playerManager;
    private Type? _networkPlayer;
    private Type? _playerSender;
    private Type? _localAvatar;

    public bool IsAvailable => _assembly != null;
    public bool IsOnline
    {
        get { try { return _networkInfo != null && ReadStatic<bool>(_networkInfo, "HasServer"); } catch { return false; } }
    }

    public void Initialize()
    {
        _assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "LabFusion");
        if (_assembly == null) { AgentLog.Info("Fusion not installed; offline mode enabled."); return; }
        _networkInfo = _assembly.GetType("LabFusion.Network.NetworkInfo");
        _playerManager = _assembly.GetType("LabFusion.Player.PlayerIDManager");
        _networkPlayer = _assembly.GetType("LabFusion.Entities.NetworkPlayer");
        _playerSender = _assembly.GetType("LabFusion.Senders.PlayerSender");
        _localAvatar = _assembly.GetType("LabFusion.Player.LocalAvatar");
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

    public IReadOnlyList<PlayerSnapshot> GetPlayerSnapshots()
    {
        var output = new List<PlayerSnapshot>();
        if (_networkPlayer?.GetField("Players", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is not IEnumerable players)
            return output;
        foreach (var player in players)
        {
            try
            {
                if (player == null) continue;
                var type = player.GetType();
                var id = type.GetProperty("PlayerID")?.GetValue(player);
                if (id == null) continue;
                var rigRefs = type.GetProperty("RigRefs")?.GetValue(player);
                var rig = rigRefs?.GetType().GetProperty("RigManager")?.GetValue(rigRefs);
                GameObject? gameObject = null;
                Vector3 position = Vector3.zero;
                try
                {
                    gameObject = rig?.GetType().GetProperty("gameObject")?.GetValue(rig) as GameObject;
                    if (gameObject != null) position = gameObject.transform.position;
                }
                catch { gameObject = null; }
                var smallId = Read<byte>(id, id.GetType(), "SmallID");
                output.Add(new PlayerSnapshot(
                    smallId,
                    Read<ulong>(id, id.GetType(), "PlatformID"),
                    SafeText(type.GetProperty("Username"), player) ?? $"Player {smallId}",
                    Read<bool>(id, id.GetType(), "IsHost"),
                    Read<bool>(id, id.GetType(), "IsMe"),
                    gameObject,
                    position));
            }
            catch (Exception ex)
            {
                AgentLog.Debug("Skipped stale Fusion player during rig transition: " + ex.GetBaseException().Message);
            }
        }
        return output;
    }

    public object GetPlayers() => GetPlayerSnapshots().Select(x => new
    {
        smallId = x.SmallId,
        platformId = x.PlatformId.ToString(),
        username = x.Username,
        isHost = x.IsHost,
        isMe = x.IsMe,
        hasRig = x.RigObject != null,
        position = new { x = x.Position.x, y = x.Position.y, z = x.Position.z }
    }).ToArray();

    public bool TryGetPlayerId(GameObject target, out byte smallId)
    {
        foreach (var player in GetPlayerSnapshots())
        {
            if (player.RigObject == null) continue;
            if (target == player.RigObject || target.transform.IsChildOf(player.RigObject.transform) || player.RigObject.transform.IsChildOf(target.transform))
            {
                smallId = player.SmallId;
                return true;
            }
        }
        smallId = 0;
        return false;
    }

    public void SwapLocalAvatar(string barcode)
    {
        if (!IsOnline || _localAvatar == null) throw new InvalidOperationException("Fusion is not online.");
        var method = _localAvatar.GetMethod("SwapAvatarCrate", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null)
                     ?? throw new MissingMethodException("LabFusion.Player.LocalAvatar.SwapAvatarCrate");
        method.Invoke(null, new object[] { barcode });
    }

    public void DamagePlayer(byte smallId, float amount, Vector3 origin, Vector3 direction)
    {
        if (!IsOnline || _playerSender == null) throw new InvalidOperationException("Fusion is not online.");
        var target = GetPlayerSnapshots().FirstOrDefault(x => x.SmallId == smallId)
                     ?? throw new InvalidOperationException($"Fusion player {smallId} was not found.");
        if (target.IsMe) throw new InvalidOperationException("Use player.damage for the local player.");
        var attack = new Attack
        {
            damage = Mathf.Max(0f, amount),
            attackType = AttackType.Blunt,
            origin = origin,
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward,
            normal = direction.sqrMagnitude > 0.0001f ? -direction.normalized : Vector3.back
        };
        var method = _playerSender.GetMethod("SendPlayerDamage", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(byte), typeof(Attack) }, null)
                     ?? throw new MissingMethodException("LabFusion.Senders.PlayerSender.SendPlayerDamage");
        method.Invoke(null, new object[] { smallId, attack });
    }

    public object GetSynchronizationReport() => new
    {
        directFusion = new[] { "avatar changes through LocalAvatar.SwapAvatarCrate", "remote-player damage through PlayerSender.SendPlayerDamage", "player/rig discovery through NetworkPlayer.Players" },
        ordinaryFusionPatches = new[] { "SpawnLab network spawning", "owned prop transforms", "grabs/releases", "gun shots", "NPC damage/death", "seats" },
        localOnly = new[] { "local health overrides", "strength/speed/vitality overrides", "Codex conversation and UI" },
        note = "Ordinary patched actions still depend on Fusion ownership and server permissions; the agent reports failures instead of claiming synchronization."
    };

    private static T? ReadStatic<T>(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        if (property?.GetValue(null) is T value) return value;
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
        return field?.GetValue(null) is T fieldValue ? fieldValue : default;
    }

    private static T Read<T>(object target, Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetValue(target) is T pv) return pv;
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(target) is T fv ? fv : default!;
    }

    private static string? SafeText(PropertyInfo? property, object target)
    {
        try { return property?.GetValue(target)?.ToString(); }
        catch { return null; }
    }
}
