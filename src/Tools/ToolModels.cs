using Newtonsoft.Json.Linq;

namespace BonelabAIAgent.Tools;

public enum AgentActionState { Queued, Validating, Executing, WaitingForNetwork, Succeeded, Failed, Cancelled }

public sealed class ToolCall
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public JObject Arguments { get; set; } = new();
}

public sealed class ToolResult
{
    public string ActionId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = "failed";
    public AgentActionState State { get; set; }
    public object? Data { get; set; }
    public string? Reason { get; set; }

    public static ToolResult Success(ToolCall call, object? data = null) => new()
    { ActionId = call.Id, Action = call.Name, Result = "success", State = AgentActionState.Succeeded, Data = data };

    public static ToolResult Failure(ToolCall call, string reason) => new()
    { ActionId = call.Id, Action = call.Name, Result = "failed", State = AgentActionState.Failed, Reason = reason };
}

public sealed class ActionRecord
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AgentActionState State { get; set; }
    public string? Error { get; set; }
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
    public DateTime? FinishedUtc { get; set; }
}
