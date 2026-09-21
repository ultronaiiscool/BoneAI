using System.Text.Json;

namespace BoneAI.Catalogs;

public sealed record AvatarCatalogItem(string Title, string Barcode, string Source, string Provider);

public sealed class AvatarCatalogProvider
{
    private readonly string _userDataDirectory;
    private readonly string _contentModsDirectory;
    private readonly object _gate = new();
    private IReadOnlyList<AvatarCatalogItem> _items = Array.Empty<AvatarCatalogItem>();
    private DateTime _lastRefreshUtc;

    public AvatarCatalogProvider(string userDataDirectory)
    {
        _userDataDirectory = userDataDirectory;
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _contentModsDirectory = Path.GetFullPath(Path.Combine(local, "..", "LocalLow", "Stress Level Zero", "BONELAB", "Mods"));
    }

    public IReadOnlyList<AvatarCatalogItem> Items
    {
        get { lock (_gate) return _items; }
    }

    public DateTime LastRefreshUtc { get { lock (_gate) return _lastRefreshUtc; } }

    public Task<IReadOnlyList<AvatarCatalogItem>> RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Refresh(cancellationToken), cancellationToken);

    public IReadOnlyList<AvatarCatalogItem> Refresh(CancellationToken cancellationToken = default)
    {
        var found = new Dictionary<string, AvatarCatalogItem>(StringComparer.OrdinalIgnoreCase);
        ReadWristHubIndex(found, cancellationToken);
        ReadPalletManifests(found, cancellationToken);
        var result = found.Values.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToArray();
        lock (_gate)
        {
            _items = result;
            _lastRefreshUtc = DateTime.UtcNow;
        }
        return result;
    }

    private void ReadWristHubIndex(Dictionary<string, AvatarCatalogItem> output, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_userDataDirectory, "WristHub", "cache", "avatar-index.json");
        if (!File.Exists(path)) return;
        using var stream = File.OpenRead(path);
        using var json = JsonDocument.Parse(stream);
        if (!json.RootElement.TryGetProperty("Avatars", out var avatars) || avatars.ValueKind != JsonValueKind.Array) return;
        foreach (var avatar in avatars.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            Add(output, Text(avatar, "Title"), Text(avatar, "Barcode"), Text(avatar, "ModName"), "WristHub index");
        }
    }

    private void ReadPalletManifests(Dictionary<string, AvatarCatalogItem> output, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_contentModsDirectory)) return;
        foreach (var path in Directory.EnumerateFiles(_contentModsDirectory, "*.pallet.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var stream = File.OpenRead(path);
                using var json = JsonDocument.Parse(stream);
                if (!json.RootElement.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Object) continue;
                var palletTitle = Path.GetFileName(Path.GetDirectoryName(path)) ?? "Installed pallet";
                foreach (var property in objects.EnumerateObject())
                {
                    var value = property.Value;
                    var type = value.TryGetProperty("isa", out var isa) ? Text(isa, "type") : string.Empty;
                    if (!type.StartsWith("crate-avatar", StringComparison.OrdinalIgnoreCase)) continue;
                    Add(output, Text(value, "title"), Text(value, "barcode"), palletTitle, "pallet manifest");
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (JsonException) { }
        }
    }

    private static void Add(Dictionary<string, AvatarCatalogItem> output, string title, string barcode, string source, string provider)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        if (string.IsNullOrWhiteSpace(title)) title = barcode;
        output[barcode] = new AvatarCatalogItem(title, barcode, source, provider);
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
