using AsyncWindowsClipboard;
using KeepClip.Core.History;
namespace KeepClip.App.Clipboard;

internal sealed class AsyncClipboardContentReader(IAsyncClipboardService library,
    Func<string, bool>? isDirectory = null, Action<Exception>? onFallback = null)
{
    public Task<string> ReadTextAsync(Func<Task<string>> windowsFallback, CancellationToken ct)
        => ReadAsync(async () => await library.GetTextAsync(), windowsFallback, ct);

    public Task<IReadOnlyList<ClipFile>> ReadFilesAsync(Func<Task<IReadOnlyList<ClipFile>>> windowsFallback, CancellationToken ct)
        => ReadAsync<IReadOnlyList<ClipFile>>(async () =>
        {
            var paths = await library.GetFileDropListAsync();
            if (paths is null) return null;
            var files = paths.Take(FileReferences.MaxItems + 1).Select(path => new ClipFile(path)).ToArray();
            // Reject the whole response: trimming a malformed CF_HDROP or
            // dropping invalid paths would silently change the copied selection.
            if (!FileReferences.Valid(files)) throw new InvalidDataException("Lista di file non valida.");
            return files.Select(file => file with
            {
                IsDirectory = isDirectory is not null ? isDirectory(file.Path)
                    : (File.GetAttributes(file.Path) & FileAttributes.Directory) != 0
            }).ToArray();
        }, windowsFallback, ct);

    async Task<T> ReadAsync<T>(Func<Task<T?>> read, Func<Task<T>> windowsFallback, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            // The library cannot interrupt a native call. Await its completion
            // before releasing the service queue, then honour cancellation.
            var result = await read();
            ct.ThrowIfCancellationRequested();
            if (result is not null) return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            ct.ThrowIfCancellationRequested();
            onFallback?.Invoke(ex);
        }
        ct.ThrowIfCancellationRequested();
        var fallback = await windowsFallback();
        ct.ThrowIfCancellationRequested();
        return fallback;
    }
}
