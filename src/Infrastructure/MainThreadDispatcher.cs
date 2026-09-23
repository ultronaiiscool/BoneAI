using System.Collections.Concurrent;

namespace BoneAI.Infrastructure;

public sealed class MainThreadDispatcher
{
    private readonly ConcurrentQueue<Action> _queue = new();

    public Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<T>(cancellationToken);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        // 0 = queued, 1 = executing/completed, 2 = cancelled before execution.
        // A cancellation after execution begins must not report that the game action was cancelled.
        var state = 0;
        if (cancellationToken.CanBeCanceled)
        {
            var registration = cancellationToken.Register(() =>
            {
                if (Interlocked.CompareExchange(ref state, 2, 0) == 0)
                    completion.TrySetCanceled(cancellationToken);
            });
            _ = completion.Task.ContinueWith(_ => registration.Dispose(),
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        _queue.Enqueue(() =>
        {
            if (Interlocked.CompareExchange(ref state, 1, 0) != 0) return;
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
