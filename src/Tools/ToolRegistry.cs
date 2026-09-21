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

    public JArray BuildOpenAiTools()
    {
        var tools = new JArray();
        foreach (var entry in _descriptions.OrderBy(x => x.Key))
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

    public JArray BuildAnthropicTools()
    {
        var tools = new JArray();
        foreach (var entry in _descriptions.OrderBy(x => x.Key))
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
