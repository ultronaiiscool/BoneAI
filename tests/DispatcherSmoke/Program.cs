using BoneAI.Infrastructure;

var dispatcher = new MainThreadDispatcher();
var executed = 0;
using (var cancelled = new CancellationTokenSource())
{
    var queued = dispatcher.InvokeAsync(() => { executed++; return 1; }, cancelled.Token);
    cancelled.Cancel();
    dispatcher.Drain();
    if (!queued.IsCanceled || executed != 0)
        throw new Exception("A cancelled queued action executed.");
}

using (var afterStart = new CancellationTokenSource())
{
    var running = dispatcher.InvokeAsync(() =>
    {
        afterStart.Cancel();
        executed++;
        return 42;
    }, afterStart.Token);
    dispatcher.Drain();
    if (await running != 42 || executed != 1)
        throw new Exception("An action cancelled after starting did not report its real result.");
}

var normal = dispatcher.InvokeAsync(() => { executed++; return 7; });
dispatcher.Drain();
if (await normal != 7 || executed != 2)
    throw new Exception("A normal queued action did not execute exactly once.");
Console.WriteLine("Dispatcher cancellation and execution tests passed.");
