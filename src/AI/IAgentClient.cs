using Newtonsoft.Json.Linq;

namespace BoneAI.AI;

public interface IAgentClient : IDisposable
{
    bool Connected { get; }
    bool NativeToolsEnabled { get; }
    string? ThreadId { get; }
    event Action<string>? StatusChanged;
    event Action<string>? DeltaReceived;
    Task ConnectAsync(string endpoint, CancellationToken cancellationToken);
    Task StartThreadAsync(string? resumeThreadId, CancellationToken cancellationToken);
    Task<string> StartTurnAsync(string prompt, JObject? outputSchema, int timeoutSeconds, CancellationToken cancellationToken);
    Task InterruptAsync(CancellationToken cancellationToken);
}
