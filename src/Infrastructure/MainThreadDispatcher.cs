using System.Collections.Concurrent;

namespace BoneAI.Infrastructure;

public sealed class MainThreadDispatcher
{
    private readonly ConcurrentQueue<Action> _queue = new();

    public Task<T> InvokeAsync<T>(Func<T> function)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(() =>
        {
            try { completion.TrySetResult(function()); }
            catch (Exception ex) { completion.TrySetException(ex); }
        });
        return completion.Task;
    }

    public void Drain(int maximum = 24)
    {
        for (var i = 0; i < maximum && _queue.TryDequeue(out var action); i++) action();
    }
}
