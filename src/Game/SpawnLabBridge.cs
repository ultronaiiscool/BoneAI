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
        var entry = Resolve(entries, barcodeOrName);
        var method = mod.GetType().GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new MissingMethodException("SpawnLab.SpawnLabMod.Spawn");
        try { method.Invoke(mod, new[] { entry.NativeEntry }); }
        catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        AgentLog.Info($"SpawnLab requested '{entry.Title}' ({entry.Barcode}).");
        return entry;
    }

    private static Entry Resolve(IReadOnlyList<Entry> entries, string query)
    {
        query = query.Trim();
        if (query.Length == 0) throw new ArgumentException("Provide a spawnable name or exact barcode.");
        var exact = entries.FirstOrDefault(x => x.Barcode.Equals(query, StringComparison.OrdinalIgnoreCase))
                    ?? entries.FirstOrDefault(x => x.Title.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        var matches = entries.Where(x => x.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                                         || x.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(6).ToArray();
        if (matches.Length == 1) return matches[0];
        if (matches.Length > 1)
            throw new InvalidOperationException("Ambiguous spawnable. Search the catalog and use an exact barcode: " +
                string.Join(", ", matches.Take(5).Select(x => x.Title + " (" + x.Barcode + ")")));
        throw new InvalidOperationException("Spawnable not found. Use spawn.list to find an installed title or barcode.");
    }
}
