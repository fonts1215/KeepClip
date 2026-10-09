namespace KeepClip.Core;

public interface IClipboardExecutor : IDisposable
{
    Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct);
}

// One operation at a time, including awaits and retries. The platform executor
// owns the apartment; clients only exchange managed clipboard snapshots.
public sealed class AsyncClipboardService : IClipboardAdapter, IDisposable
{
    readonly IClipboardAdapter backend;
    readonly IClipboardExecutor executor;
    readonly Func<uint> sequence;
    readonly SemaphoreSlim operations = new(1, 1);
    readonly CancellationTokenSource lifetime = new();
    readonly CancellationToken stopping;
    int disposed;

    public AsyncClipboardService(IClipboardAdapter backend, IClipboardExecutor executor, Func<uint> sequence)
    {
        this.backend = backend;
        this.executor = executor;
        this.sequence = sequence;
        stopping = lifetime.Token;
    }

    public Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default)
        => RunAsync(token => ClipboardReader.ReadWithRetryAsync(backend, token), ct);

    public Task WriteAsync(History.ClipContent content, Guid operationId, CancellationToken ct = default)
        => WriteIfCurrentAsync(content, operationId, static () => true, ct);

    public async Task WriteIfCurrentAsync(History.ClipContent content, Guid operationId, Func<bool> isCurrent, CancellationToken ct = default)
    {
        var expectedSequence = sequence();
        await RunAsync(async token =>
        {
            // Recheck in the apartment, after both the queue and dispatch waits.
            if (sequence() != expectedSequence) throw new ClipboardWriteSupersededException();
            if (!isCurrent()) throw new ClipboardRequestCancelledException();
            await backend.WriteIfCurrentAsync(content, operationId, isCurrent, token);
            return true;
        }, ct).ConfigureAwait(false);
    }

    async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, stopping);
        var token = linked.Token;
        await operations.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await executor.RunAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                return operation(token);
            }, token).ConfigureAwait(false);
        }
        finally { operations.Release(); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel();
        executor.Dispose();
        lifetime.Dispose();
        // Do not dispose the semaphore while cancelled waiters are unwinding.
    }
}

public sealed class ClipboardRequestCancelledException : OperationCanceledException
{
    public ClipboardRequestCancelledException() : base("La richiesta di copia non è più attuale.") { }
}
