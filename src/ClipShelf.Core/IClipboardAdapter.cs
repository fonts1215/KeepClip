using ClipShelf.Core.History;
namespace ClipShelf.Core;
public sealed record ClipboardSnapshot(ClipContent Content, uint Sequence, bool IsOwnWrite = false);
public interface IClipboardAdapter
{
    Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default);
    Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default);
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
