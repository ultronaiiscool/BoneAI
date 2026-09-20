using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using BonelabAIAgent.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BonelabAIAgent.AI;

public sealed class CodexAppServerClient : IDisposable
{
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _lifetime;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JObject>> _requests = new();
    private long _requestId;
    private string _assistantText = string.Empty;
    private TaskCompletionSource<string>? _turn;

    public bool Connected => _socket?.State == WebSocketState.Open;
    public string? ThreadId { get; private set; }
    public event Action<string>? StatusChanged;
    public event Action<string>? DeltaReceived;

    public async Task ConnectAsync(string endpoint, CancellationToken cancellationToken)
    {
        DisposeSocket();
        _socket = new ClientWebSocket();
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        StatusChanged?.Invoke("Connecting");
        await _socket.ConnectAsync(new Uri(endpoint), cancellationToken).ConfigureAwait(false);
        _ = Task.Run(() => ReceiveLoopAsync(_lifetime.Token));
        await RequestAsync("initialize", new JObject
        {
            ["clientInfo"] = new JObject { ["name"] = "bonelab-ai-agent", ["title"] = "BONELAB AI Agent", ["version"] = "1.0.4" }
        }, cancellationToken).ConfigureAwait(false);
        await SendAsync(new JObject { ["method"] = "initialized", ["params"] = new JObject() }, cancellationToken).ConfigureAwait(false);
        StatusChanged?.Invoke("Connected");
    }

    public async Task StartThreadAsync(string? resumeThreadId, CancellationToken cancellationToken)
    {
        JObject response;
        if (!string.IsNullOrWhiteSpace(resumeThreadId))
        {
            try { response = await RequestAsync("thread/resume", new JObject { ["threadId"] = resumeThreadId }, cancellationToken).ConfigureAwait(false); }
            catch
            {
                response = await RequestAsync("thread/start", new JObject(), cancellationToken).ConfigureAwait(false);
            }
        }
        else response = await RequestAsync("thread/start", new JObject(), cancellationToken).ConfigureAwait(false);
        ThreadId = response.SelectToken("result.thread.id")?.Value<string>() ?? response.SelectToken("result.threadId")?.Value<string>();
        if (string.IsNullOrWhiteSpace(ThreadId)) throw new InvalidOperationException("Codex did not return a thread ID.");
    }

    public async Task<string> StartTurnAsync(string prompt, JObject outputSchema, int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (!Connected || string.IsNullOrWhiteSpace(ThreadId)) throw new InvalidOperationException("Codex App Server is not connected.");
        _assistantText = string.Empty;
        _turn = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        using var registration = timeout.Token.Register(() => _turn.TrySetCanceled(timeout.Token));
        await RequestAsync("turn/start", new JObject
        {
            ["threadId"] = ThreadId,
            ["input"] = new JArray(new JObject { ["type"] = "text", ["text"] = prompt }),
            ["outputSchema"] = outputSchema
        }, timeout.Token).ConfigureAwait(false);
        return await _turn.Task.ConfigureAwait(false);
    }

    public async Task InterruptAsync(CancellationToken cancellationToken)
    {
        if (Connected && ThreadId != null)
            await RequestAsync("turn/interrupt", new JObject { ["threadId"] = ThreadId }, cancellationToken).ConfigureAwait(false);
        _turn?.TrySetCanceled(cancellationToken);
    }

    private async Task<JObject> RequestAsync(string method, JObject parameters, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _requestId);
        var completion = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests[id] = completion;
        await SendAsync(new JObject { ["id"] = id, ["method"] = method, ["params"] = parameters }, cancellationToken).ConfigureAwait(false);
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return await completion.Task.ConfigureAwait(false);
    }

    private async Task SendAsync(JObject message, CancellationToken cancellationToken)
    {
        if (_socket == null || _socket.State != WebSocketState.Open) throw new InvalidOperationException("WebSocket is not open.");
        var bytes = Encoding.UTF8.GetBytes(message.ToString(Formatting.None));
        await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (_socket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var stream = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("Codex App Server closed the connection.");
                    stream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                HandleMessage(JObject.Parse(Encoding.UTF8.GetString(stream.ToArray())));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AgentLog.Exception("Codex receive", ex);
            StatusChanged?.Invoke("Disconnected: " + ex.GetBaseException().Message);
            _turn?.TrySetException(ex);
        }
    }

    private void HandleMessage(JObject message)
    {
        if (message["id"]?.Value<long?>() is long id && _requests.TryRemove(id, out var request))
        {
            if (message["error"] != null) request.TrySetException(new InvalidOperationException(message["error"]!.ToString(Formatting.None)));
            else request.TrySetResult(message);
            return;
        }
        var method = message["method"]?.Value<string>() ?? string.Empty;
        if (method == "item/agentMessage/delta")
        {
            var delta = message.SelectToken("params.delta")?.Value<string>() ?? string.Empty;
            _assistantText += delta;
            DeltaReceived?.Invoke(delta);
        }
        else if (method == "turn/completed")
        {
            var status = message.SelectToken("params.turn.status")?.Value<string>();
            if (status == "failed") _turn?.TrySetException(new InvalidOperationException(message.SelectToken("params.turn.error.message")?.Value<string>() ?? "Codex turn failed."));
            else _turn?.TrySetResult(_assistantText);
        }
        else if (method == "turn/failed") _turn?.TrySetException(new InvalidOperationException(message.SelectToken("params.error.message")?.Value<string>() ?? "Codex turn failed."));
    }

    private void DisposeSocket()
    {
        try { _lifetime?.Cancel(); _socket?.Dispose(); } catch { }
        _lifetime?.Dispose();
        _lifetime = null;
        _socket = null;
    }

    public void Dispose() => DisposeSocket();
}
