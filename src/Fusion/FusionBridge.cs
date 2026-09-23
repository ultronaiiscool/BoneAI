using System.Collections;
using System.Reflection;
using BoneAI.Infrastructure;
using Il2CppSLZ.Marrow.Combat;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Interaction;
using UnityEngine;

namespace BoneAI.Fusion;

public sealed class FusionBridge
{
    public sealed record PlayerSnapshot(byte SmallId, ulong PlatformId, string Username, bool IsHost, bool IsMe, GameObject? RigObject, Vector3 Position);

    private Assembly? _assembly;
    private Type? _networkInfo;
    private Type? _playerManager;
    private Type? _networkPlayer;
    private Type? _playerSender;
    private Type? _localAvatar;
    private Type? _sceneManager;
    private object? _marrowEntityCache;
    private object? _pooleeCache;
    private MethodInfo? _marrowCacheGet;
    private MethodInfo? _pooleeCacheGet;
    private IReadOnlyList<PlayerSnapshot> _playerCache = Array.Empty<PlayerSnapshot>();
    private float _playerCacheExpiresAt;

    public bool IsAvailable => _assembly != null;
    public bool IsOnline
    {
        get { try { return _networkInfo != null && ReadStatic<bool>(_networkInfo, "HasServer"); } catch { return false; } }
    }
    public bool IsSceneNetworked => IsOnline && _sceneManager != null && ReadStatic<bool>(_sceneManager, "IsLevelNetworked");

    public void Initialize()
    {
        _assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "LabFusion");
        if (_assembly == null) { AgentLog.Info("Fusion not installed; offline mode enabled."); return; }
        _networkInfo = _assembly.GetType("LabFusion.Network.NetworkInfo");
        _playerManager = _assembly.GetType("LabFusion.Player.PlayerIDManager");
        _networkPlayer = _assembly.GetType("LabFusion.Entities.NetworkPlayer");
        _playerSender = _assembly.GetType("LabFusion.Senders.PlayerSender");
        _localAvatar = _assembly.GetType("LabFusion.Player.LocalAvatar");
        _sceneManager = _assembly.GetType("LabFusion.Scene.NetworkSceneManager");
        _marrowEntityCache = _assembly.GetType("LabFusion.Entities.IMarrowEntityExtender")?
            .GetField("Cache", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        _pooleeCache = _assembly.GetType("LabFusion.Marrow.Extenders.PooleeExtender")?
            .GetField("Cache", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        _marrowCacheGet = _marrowEntityCache?.GetType().GetMethod("Get", BindingFlags.Public | BindingFlags.Instance);
        _pooleeCacheGet = _pooleeCache?.GetType().GetMethod("Get", BindingFlags.Public | BindingFlags.Instance);
        AgentLog.Info($"Fusion bridge loaded for {_assembly.GetName().Version}.");
    }

    /// <summary>Fail closed before changing a multiplayer-visible object locally.</summary>
    public string RequireLocalObjectAuthority(GameObject target)
    {
        if (!IsSceneNetworked) return IsOnline ? "local-only (Fusion level not networked)" : "offline";
        var marrow = MarrowEntity.Cache.Get(target) ?? target.GetComponentInParent<MarrowEntity>() ?? target.GetComponentInChildren<MarrowEntity>();
        object? entity = marrow == null ? null : _marrowCacheGet?.Invoke(_marrowEntityCache, new object[] { marrow });
        if (entity == null)
        {
            var poolee = target.GetComponentsInChildren<Component>()
                .FirstOrDefault(x => x.GetType().Name == "Poolee");
            if (poolee != null) entity = _pooleeCacheGet?.Invoke(_pooleeCache, new object[] { poolee });
        }
        if (entity == null)
            throw new InvalidOperationException("Fusion has no network entity for this object; local manipulation was blocked to avoid desync.");
        if (entity.GetType().GetProperty("IsRegistered")?.GetValue(entity) is not true)
            throw new InvalidOperationException("Fusion network entity is not registered yet.");
        if (entity.GetType().GetProperty("IsOwner")?.GetValue(entity) is not true)
            throw new InvalidOperationException("The local player does not own this Fusion network entity.");
        var id = entity.GetType().GetProperty("ID")?.GetValue(entity);
        return $"Fusion local owner (entity {id}); peer replication not independently confirmed";
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
        if (Time.unscaledTime < _playerCacheExpiresAt) return _playerCache;
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
        _playerCache = output;
        _playerCacheExpiresAt = Time.unscaledTime + 0.25f;
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
        ordinaryFusionPatches = new[] { "SpawnLab network spawn requests", "locally owned prop transforms", "grabs/releases", "gun shots", "NPC damage/death", "seats" },
        localOnly = new[] { "local health overrides", "strength/speed/vitality overrides", "Codex conversation and UI" },
        note = "Object mutation now requires confirmed local Fusion ownership. A local API call is not proof that peers received the result; spawn and asynchronous actions remain pending until independently observed."
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
