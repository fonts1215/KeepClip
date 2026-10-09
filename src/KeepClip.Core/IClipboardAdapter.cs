using KeepClip.Core.History;
namespace KeepClip.Core;
public sealed record ClipboardSnapshot(ClipContent Content, uint Sequence, bool IsOwnWrite = false, long? CaptureGeneration = null, ClipSource? Source = null);
public interface IClipboardAdapter
{
    Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default);
    Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default);
    // A current-request guard must be safe to evaluate on the clipboard worker.
    Task WriteIfCurrentAsync(ClipContent content, Guid operationId, Func<bool> isCurrent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!isCurrent()) throw new ClipboardRequestCancelledException();
        return WriteAsync(content, operationId, ct);
    }
}
public static class ClipboardReader
{
    public static async Task<ClipboardSnapshot?> ReadWithRetryAsync(IClipboardAdapter adapter, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await adapter.ReadAsync(ct); }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 2)
            { await Task.Delay(attempt == 0 ? 50 : 100, ct); }
        }
    }
}
