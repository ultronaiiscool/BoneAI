using BonelabAIAgent.Tools;

namespace BonelabAIAgent.AI;

public sealed class StructuredReply
{
    public string Message { get; set; } = string.Empty;
    public List<ToolCall> ToolCalls { get; set; } = new();
}
