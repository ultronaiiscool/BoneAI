using System.Collections;
using System.Reflection;
using BoneAI.Infrastructure;
using MelonLoader;

namespace BoneAI.Game;

public sealed class SpawnLabBridge
{
    public sealed record Entry(string Title, string Barcode, string Source, string Category, bool Downloaded, object NativeEntry);
    private object? _mod;
    private IReadOnlyList<Entry>? _entries;
    private float _entriesExpireAt;

    private object? FindMod() => _mod ??= MelonBase.RegisteredMelons.FirstOrDefault(x => x.GetType().FullName == "SpawnLab.SpawnLabMod");

    public bool Available => FindMod() != null;

    public IReadOnlyList<Entry> GetEntries()
    {
        if (_entries != null && UnityEngine.Time.unscaledTime < _entriesExpireAt) return _entries;
        var mod = FindMod();
        if (mod == null) return Array.Empty<Entry>();
        var field = mod.GetType().GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(mod) is not IEnumerable entries) return Array.Empty<Entry>();
        var output = new List<Entry>();
        foreach (var entry in entries)
        {
            if (entry == null) continue;
            var type = entry.GetType();
            string Read(string name) => type.GetProperty(name)?.GetValue(entry)?.ToString() ?? string.Empty;
            var downloaded = type.GetProperty("IsDownloaded")?.GetValue(entry) is true;
            output.Add(new Entry(Read("Title"), Read("Barcode"), Read("SourceName"), Read("Category"), downloaded, entry));
        }
        _entries = output;
        _entriesExpireAt = UnityEngine.Time.unscaledTime + 5f;
        return output;
    }

    public void Refresh()
    {
        var mod = FindMod() ?? throw new InvalidOperationException("SpawnLab is not loaded.");
        var method = mod.GetType().GetMethod("RebuildMenu", BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new MissingMethodException("SpawnLab.SpawnLabMod.RebuildMenu");
        method.Invoke(mod, null);
        _entries = null; _entriesExpireAt = 0;
    }

    public Entry Spawn(string barcodeOrName)
    {
        var mod = FindMod() ?? throw new InvalidOperationException("SpawnLab is not loaded.");
        var entries = GetEntries();
        var entry = entries.FirstOrDefault(x => string.Equals(x.Barcode, barcodeOrName, StringComparison.OrdinalIgnoreCase))
                    ?? entries.OrderBy(x => Score(x.Title, barcodeOrName)).FirstOrDefault()
                    ?? throw new InvalidOperationException("SpawnLab has no spawnable entries.");
        var method = mod.GetType().GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new MissingMethodException("SpawnLab.SpawnLabMod.Spawn");
        try { method.Invoke(mod, new[] { entry.NativeEntry }); }
        catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        AgentLog.Info($"SpawnLab requested '{entry.Title}' ({entry.Barcode}).");
        return entry;
    }

    private static int Score(string value, string query)
    {
        value = value.ToLowerInvariant(); query = query.ToLowerInvariant();
        if (value == query) return 0;
        if (value.Contains(query)) return 1 + value.IndexOf(query, StringComparison.Ordinal);
        return Math.Abs(value.Length - query.Length) + 20;
    }
}
