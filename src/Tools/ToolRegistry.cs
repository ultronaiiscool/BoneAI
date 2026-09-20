using System.Collections.Concurrent;
using BonelabAIAgent.Infrastructure;
using Newtonsoft.Json;

namespace BonelabAIAgent.Tools;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, Func<ToolCall, ToolResult>> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _descriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ActionRecord> _actions = new();
    private readonly MainThreadDispatcher _dispatcher;

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
            return result;
        }
    }

    public IReadOnlyCollection<ActionRecord> RecentActions => _actions.Values.OrderByDescending(x => x.StartedUtc).Take(20).ToArray();

    public string BuildCatalogJson() => JsonConvert.SerializeObject(_descriptions.Select(x => new { name = x.Key, description = x.Value }), Formatting.None);
}
