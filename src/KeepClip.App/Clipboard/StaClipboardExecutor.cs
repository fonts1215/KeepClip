using KeepClip.Core;
using System.Windows.Threading;
namespace KeepClip.App.Clipboard;

internal sealed class StaClipboardExecutor : IClipboardExecutor
{
    readonly TaskCompletionSource<Dispatcher> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly object lifetime = new();
    bool disposed;
    int active;

    public StaClipboardExecutor()
    {
        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                ready.SetResult(dispatcher);
                Dispatcher.Run();
            }
            catch (Exception ex) { ready.TrySetException(ex); }
        }) { IsBackground = true, Name = "KeepClip clipboard STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (lifetime)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            active++;
        }
        try
        {
            var dispatcher = await ready.Task.WaitAsync(ct).ConfigureAwait(false);
            var pending = dispatcher.InvokeAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                return action();
            }, DispatcherPriority.Normal, ct);
            // Wait until the operation actually ends. A Windows/provider read
            // cannot be forcibly cancelled; releasing early would allow overlap.
            return await pending.Task.Unwrap().ConfigureAwait(false);
        }
        finally
        {
            lock (lifetime)
            {
                active--;
                if (disposed && active == 0) StopDispatcher();
            }
        }
    }

    public void Dispose()
    {
        lock (lifetime)
        {
            if (disposed) return;
            disposed = true;
            if (active == 0) StopDispatcher();
        }
    }

    void StopDispatcher() => _ = ready.Task.ContinueWith(task =>
    {
        if (task.IsCompletedSuccessfully) task.Result.BeginInvokeShutdown(DispatcherPriority.Background);
    }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
}
