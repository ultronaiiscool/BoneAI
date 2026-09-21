using BoneAI.Infrastructure;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;

namespace BoneAI.AI;

public sealed class SavedConversation
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Title { get; set; } = "New conversation";
    public string Preview { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ConversationStore
{
    private readonly string _path = Path.Combine(MelonEnvironment.UserDataDirectory, "BoneAI", "conversations.json");
    private readonly object _gate = new();
    private List<SavedConversation> _items = new();

    public ConversationStore() => Load();

    public IReadOnlyList<SavedConversation> List()
    {
        lock (_gate) return _items.OrderByDescending(x => x.UpdatedUtc).Select(Clone).ToArray();
    }

    public void Touch(string id, string provider, string? title = null, string? preview = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        lock (_gate)
        {
            var item = _items.FirstOrDefault(x => x.Id == id && x.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));
            if (item == null) { item = new SavedConversation { Id = id, Provider = provider }; _items.Add(item); }
            if (!string.IsNullOrWhiteSpace(title) && item.Title == "New conversation") item.Title = Trim(title, 56);
            if (preview != null) item.Preview = Trim(preview, 120);
            item.UpdatedUtc = DateTime.UtcNow;
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path)) _items = JsonConvert.DeserializeObject<List<SavedConversation>>(File.ReadAllText(_path)) ?? new();
        }
        catch (Exception ex) { AgentLog.Warn("Could not load saved conversations: " + ex.Message); }
    }

    private void Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); File.WriteAllText(_path, JsonConvert.SerializeObject(_items, Formatting.Indented)); }
        catch (Exception ex) { AgentLog.Warn("Could not save conversations: " + ex.Message); }
    }

    private static SavedConversation Clone(SavedConversation x) => new() { Id = x.Id, Provider = x.Provider, Title = x.Title, Preview = x.Preview, CreatedUtc = x.CreatedUtc, UpdatedUtc = x.UpdatedUtc };
    private static string Trim(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
