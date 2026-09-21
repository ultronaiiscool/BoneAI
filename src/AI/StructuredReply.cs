using BoneAI.Tools;

namespace BoneAI.AI;

public sealed class StructuredReply
{
    public string Message { get; set; } = string.Empty;
    public List<ToolCall> ToolCalls { get; set; } = new();
}
