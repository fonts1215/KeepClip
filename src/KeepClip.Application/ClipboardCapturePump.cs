namespace KeepClip.Application;

// Notify and its continuations run on the monitor's dispatcher.
public sealed class ClipboardCapturePump(HistoryService history, Func<CancellationToken, Task<ClipboardSnapshot?>> read) : IDisposable
{
    readonly CancellationTokenSource lifetime = new();
    bool reading, pending;
    public event Action<ClipboardSnapshot, CaptureResult>? Captured;
    public event Action? Empty;
    public event Action<Exception>? Failed;

    public void Notify()
    {
        if (history.IsPaused || lifetime.IsCancellationRequested) return;
        pending = true;
        if (!reading) _ = ReadPendingAsync(lifetime.Token);
    }

    async Task ReadPendingAsync(CancellationToken ct)
    {
        reading = true;
        try
        {
            while (pending && !ct.IsCancellationRequested)
            {
                pending = false;
                var generation = history.CaptureGeneration;
                if (history.IsPaused) continue;
                try
                {
                    var snapshot = await read(ct);
                    if (snapshot is null) Empty?.Invoke();
                    // Submit while the snapshot is fresh. HistoryService serializes saves
                    // with selection copies and other history changes.
                    else _ = SaveAsync(snapshot with { CaptureGeneration = generation }, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                catch (Exception ex) { Failed?.Invoke(ex); }
            }
        }
        finally { reading = false; }
    }

    async Task SaveAsync(ClipboardSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            var result = await history.CaptureAsync(snapshot, ct);
            if (!ct.IsCancellationRequested) Captured?.Invoke(snapshot, result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { Failed?.Invoke(ex); }
    }

    public void Dispose()
    {
        if (lifetime.IsCancellationRequested) return;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
