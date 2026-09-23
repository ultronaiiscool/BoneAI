using BoneAI.Fusion;
using BoneAI.Infrastructure;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Warehouse;
using UnityEngine;

namespace BoneAI.Game;

/// <summary>BoneAI-owned catalog and spawn lifecycle; no SpawnLab assembly or source is bundled.</summary>
public sealed class SpawnController
{
    public sealed class Attempt
    {
        public string ActionId { get; init; } = string.Empty;
        public SpawnCatalogEntry Entry { get; init; } = null!;
        public bool NetworkRequested { get; init; }
        public DateTime RequestedUtc { get; init; } = DateTime.UtcNow;
        public string State { get; set; } = "requested";
        public string? Error { get; set; }
        public GameObject? Spawned { get; set; }
        public string? NetworkEntityId { get; set; }
        public uint? NetworkTrackerId { get; set; }
    }

    private readonly FusionBridge _fusion;
    private IReadOnlyList<SpawnCatalogEntry>? _entries;
    private float _expiresAt;
    private readonly Dictionary<string, Attempt> _attempts = new(StringComparer.Ordinal);

    public SpawnController(FusionBridge fusion) => _fusion = fusion;

    public IReadOnlyList<SpawnCatalogEntry> GetEntries()
    {
        if (_entries != null && Time.unscaledTime < _expiresAt) return _entries;
        var warehouse = AssetWarehouse.Instance;
        if (warehouse == null || !warehouse.InitialLoaded) return Array.Empty<SpawnCatalogEntry>();
        var found = new Dictionary<string, SpawnCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var crate in warehouse.GetCrates())
        {
            if (crate is not SpawnableCrate spawnable) continue;
            var barcode = spawnable.Barcode?.ID;
            if (string.IsNullOrWhiteSpace(barcode)) continue;
            var title = string.IsNullOrWhiteSpace(spawnable.Title) ? barcode : spawnable.Title;
            var source = spawnable.Pallet?.Title ?? "Unknown pallet";
            found[barcode] = new SpawnCatalogEntry(title, barcode, source, Category(title));
        }
        _entries = found.Values.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToArray();
        _expiresAt = Time.unscaledTime + 20f;
        return _entries;
    }

    public int Refresh()
    {
        _entries = null;
        _expiresAt = 0;
        return GetEntries().Count;
    }

    public Attempt Spawn(string actionId, string query, Vector3 position, Quaternion rotation)
    {
        if (GetEntries().Count == 0) throw new InvalidOperationException("The Marrow spawnable warehouse is not loaded yet.");
        var entry = SpawnCatalogMatcher.Resolve(GetEntries(), query);
        if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z))
            throw new ArgumentException("Spawn position must contain finite coordinates.");
        if (_fusion.IsOnline && !_fusion.IsSceneNetworked)
            throw new InvalidOperationException("Fusion is online but this level is not networked; refusing a local-only spawn.");
        if (_attempts.Values.Count(x => x.State == "requested" && (DateTime.UtcNow - x.RequestedUtc).TotalSeconds < 30) >= 8)
            throw new InvalidOperationException("Too many unconfirmed spawn requests. Check spawn.status before sending more.");

        var attempt = new Attempt { ActionId = actionId, Entry = entry, NetworkRequested = _fusion.IsSceneNetworked };
        _attempts[actionId] = attempt;
        PruneAttempts();
        var spawnable = new Spawnable { crateRef = new SpawnableCrateReference(entry.Barcode) };
        try
        {
            if (attempt.NetworkRequested)
            {
                attempt.NetworkTrackerId = _fusion.RequestNetworkSpawn(spawnable, position, rotation, (go, entityId) =>
                {
                    attempt.Spawned = go;
                    attempt.NetworkEntityId = entityId;
                    attempt.State = go == null ? "unconfirmed" : "confirmed_local";
                });
            }
            else
            {
                AssetSpawner.Register(spawnable);
                var scale = new Il2CppSystem.Nullable<Vector3>(Vector3.zero) { hasValue = false };
                var group = new Il2CppSystem.Nullable<int>(0) { hasValue = false };
                var awaiter = AssetSpawner.SpawnAsync(spawnable, position, rotation, scale, null, false, group, null, null).GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    try
                    {
                        var poolee = awaiter.GetResult();
                        attempt.Spawned = poolee == null ? null : poolee.gameObject;
                        attempt.State = attempt.Spawned == null ? "failed" : "confirmed_local";
                        if (attempt.Spawned == null) attempt.Error = "The game spawner returned no object.";
                    }
                    catch (Exception ex) { attempt.State = "failed"; attempt.Error = ex.Message; AgentLog.Exception("spawn callback", ex); }
                });
            }
            AgentLog.Info($"Spawn request {actionId}: {entry.Title} ({entry.Barcode}); network={attempt.NetworkRequested}.");
            return attempt;
        }
        catch
        {
            _attempts.Remove(actionId);
            throw;
        }
    }

    public Attempt? GetAttempt(string actionId) => _attempts.TryGetValue(actionId, out var attempt) ? attempt : null;

    public void SweepExpiredRequests()
    {
        foreach (var attempt in _attempts.Values)
        {
            if (attempt.State != "requested" || !attempt.NetworkRequested ||
                (DateTime.UtcNow - attempt.RequestedUtc).TotalSeconds < 120) continue;
            if (attempt.NetworkTrackerId is uint tracker)
            {
                try { _fusion.ExpireNetworkSpawnCallback(tracker); }
                catch (Exception ex) { AgentLog.Warn("Could not expire Fusion spawn callback: " + ex.GetBaseException().Message); }
            }
            attempt.State = "unconfirmed_timeout";
            attempt.Error = "Fusion did not return a local spawn callback within two minutes. The request may have been rejected, lost, or still loading on peers.";
        }
    }

    private void PruneAttempts()
    {
        if (_attempts.Count <= 256) return;
        foreach (var stale in _attempts.Values.OrderByDescending(x => x.RequestedUtc).Skip(128).ToArray())
            _attempts.Remove(stale.ActionId);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static string Category(string title)
    {
        var value = title.ToLowerInvariant();
        if (value.Contains("npc") || value.Contains("ford") || value.Contains("nullbody")) return "NPCs";
        if (value.Contains("car") || value.Contains("kart") || value.Contains("vehicle")) return "Vehicles";
        if (value.Contains("rifle") || value.Contains("pistol") || value.Contains("gun")) return "Guns";
        if (value.Contains("sword") || value.Contains("bat") || value.Contains("knife")) return "Melee";
        if (value.Contains("gadget") || value.Contains("tool")) return "Gadgets";
        return "Other";
    }
}
