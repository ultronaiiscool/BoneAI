using System.Collections.Concurrent;
using BoneAI.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BoneAI.Tools;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, Func<ToolCall, ToolResult>> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _descriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ActionRecord> _actions = new();
    private readonly MainThreadDispatcher _dispatcher;
    public event Action<string>? ActionChanged;
    public int Count => _handlers.Count;

    public ToolRegistry(MainThreadDispatcher dispatcher) => _dispatcher = dispatcher;

    public void Register(string name, string description, Func<ToolCall, ToolResult> handler)
    {
        _handlers[name] = handler;
        _descriptions[name] = description;
    }

    public async Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken)
    {
        var record = new ActionRecord { Id = call.Id, Name = call.Name, State = AgentActionState.Queued };
        _actions[call.Id] = record;
        if (!_handlers.TryGetValue(call.Name, out var handler)) return Finish(ToolResult.Failure(call, "Unknown tool."));
        if (cancellationToken.IsCancellationRequested) return Finish(new ToolResult { ActionId = call.Id, Action = call.Name, Result = "cancelled", State = AgentActionState.Cancelled });

        record.State = AgentActionState.Validating;
        try
        {
            record.State = AgentActionState.Executing;
            ActionChanged?.Invoke(call.Name);
            AgentLog.Info($"Tool request {call.Name} ({call.Id})");
            return Finish(await _dispatcher.InvokeAsync(() => handler(call)).WaitAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return Finish(new ToolResult { ActionId = call.Id, Action = call.Name, Result = "cancelled", State = AgentActionState.Cancelled });
        }
        catch (Exception ex)
        {
            AgentLog.Exception($"tool {call.Name}", ex);
            return Finish(ToolResult.Failure(call, ex.GetBaseException().Message));
        }

        ToolResult Finish(ToolResult result)
        {
            record.State = result.State;
            record.Error = result.Reason;
            record.FinishedUtc = DateTime.UtcNow;
            ActionChanged?.Invoke("Idle");
            return result;
        }
    }

    public IReadOnlyCollection<ActionRecord> RecentActions => _actions.Values.OrderByDescending(x => x.StartedUtc).Take(20).ToArray();

    public Task<T> OnGameThreadAsync<T>(Func<T> action) => _dispatcher.InvokeAsync(action);

    public string BuildCatalogJson() => JsonConvert.SerializeObject(_descriptions.Select(x => new { name = x.Key, description = x.Value }), Formatting.None);

    public string[] SelectForPrompt(string prompt, int limit)
    {
        var words = new HashSet<string>((prompt ?? string.Empty).ToLowerInvariant().Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ':', ';', '/', '_', '-' }, StringSplitOptions.RemoveEmptyEntries));
        var mandatory = new[] { "tools.search", "tools.list_categories", "player.get_state", "world.look_at_target", "world.find_nearest", "fusion.get_session" };
        return _descriptions
            .Select(x => new
            {
                x.Key,
                Score = mandatory.Contains(x.Key, StringComparer.OrdinalIgnoreCase) ? 10000 :
                    words.Sum(word => (x.Key + " " + x.Value).Contains(word, StringComparison.OrdinalIgnoreCase) ? Math.Min(word.Length, 12) : 0)
            })
            .OrderByDescending(x => x.Score).ThenBy(x => x.Key)
            .Take(Math.Clamp(limit, 8, _descriptions.Count))
            .Select(x => x.Key).ToArray();
    }

    public void RegisterDiscoveryTools()
    {
        Register("tools.search", "Search BoneAI's full tool catalog. arguments: query, optional limit. Use this when the needed tool is not currently visible.", call =>
        {
            var query = call.Arguments["query"]?.Value<string>() ?? string.Empty;
            var limit = Math.Clamp(call.Arguments["limit"]?.Value<int>() ?? 30, 1, 100);
            var words = query.ToLowerInvariant().Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var found = _descriptions.Where(x => x.Key != "tools.search")
                .Select(x => new { name = x.Key, description = x.Value, score = words.Sum(w => (x.Key + " " + x.Value).Contains(w, StringComparison.OrdinalIgnoreCase) ? w.Length : 0) })
                .OrderByDescending(x => x.score).ThenBy(x => x.name).Take(limit).ToArray();
            return ToolResult.Success(call, new { totalTools = Count, matches = found });
        });
        Register("tools.list_categories", "List BoneAI tool namespaces and counts.", call => ToolResult.Success(call,
            _descriptions.Keys.GroupBy(x => x.Split('.')[0]).OrderBy(x => x.Key).Select(x => new { category = x.Key, count = x.Count() }).ToArray()));
        Register("tools.get_recent_actions", "Get recent authoritative action states and failures.", call => ToolResult.Success(call, RecentActions));
    }

    public JArray BuildOpenAiTools(IEnumerable<string>? selectedNames = null)
    {
        var tools = new JArray();
        var selected = selectedNames == null ? null : new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _descriptions.Where(x => selected == null || selected.Contains(x.Key)).OrderBy(x => x.Key))
            tools.Add(new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = ToExternalName(entry.Key),
                    ["description"] = entry.Value,
                    ["parameters"] = new JObject { ["type"] = "object", ["additionalProperties"] = true }
                }
            });
        return tools;
    }

    public JArray BuildAnthropicTools(IEnumerable<string>? selectedNames = null)
    {
        var tools = new JArray();
        var selected = selectedNames == null ? null : new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _descriptions.Where(x => selected == null || selected.Contains(x.Key)).OrderBy(x => x.Key))
            tools.Add(new JObject
            {
                ["name"] = ToExternalName(entry.Key),
                ["description"] = entry.Value,
                ["input_schema"] = new JObject { ["type"] = "object", ["additionalProperties"] = true }
            });
        return tools;
    }

    public static string ToExternalName(string name) => name.Replace(".", "__dot__", StringComparison.Ordinal);
    public static string FromExternalName(string name) => name.Replace("__dot__", ".", StringComparison.Ordinal);

    public JArray BuildDynamicTools()
    {
        var tools = new JArray();
        foreach (var group in _descriptions.OrderBy(x => x.Key).GroupBy(x => x.Key.Split('.')[0], StringComparer.OrdinalIgnoreCase))
        {
            var members = new JArray();
            foreach (var entry in group)
            {
                var separator = entry.Key.IndexOf('.');
                members.Add(new JObject
                {
                    ["type"] = "function",
                    ["name"] = separator < 0 ? entry.Key : entry.Key[(separator + 1)..],
                    ["description"] = entry.Value,
                    ["inputSchema"] = new JObject { ["type"] = "object", ["additionalProperties"] = true }
                });
            }
            tools.Add(new JObject
            {
                ["type"] = "namespace",
                ["name"] = group.Key,
                ["description"] = $"BONELAB {group.Key} tools. These execute only inside the game and return authoritative results.",
                ["tools"] = members
            });
        }
        return tools;
    }
}
