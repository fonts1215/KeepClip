using AsyncWindowsClipboard;
using KeepClip.App.Clipboard;
using KeepClip.Core.History;
namespace KeepClip.Tests;

public sealed class AsyncClipboardContentReaderTests
{
    [Fact]
    public async Task UnicodeTextIsReadThroughTheLibrary()
    {
        var library = new Library { Text = "Caffè 日本語 👋" };
        var reader = new AsyncClipboardContentReader(library);
        var result = await reader.ReadTextAsync(() => throw new InvalidOperationException("unexpected fallback"), default);
        Assert.Equal(library.Text, result);
        Assert.Equal(1, library.TextReads);
    }

    [Fact]
    public async Task FileAndFolderPathsAreReadThroughTheLibrary()
    {
        var library = new Library { Paths = ["C:\\folder", "C:\\file 日本語.txt"] };
        var reader = new AsyncClipboardContentReader(library, path => path == "C:\\folder");
        var result = await reader.ReadFilesAsync(() => throw new InvalidOperationException("unexpected fallback"), default);
        Assert.Equal([new ClipFile("C:\\folder", true), new ClipFile("C:\\file 日本語.txt", false)], result);
        Assert.Equal(1, library.FileReads);
    }

    [Fact]
    public async Task InvalidFileDropDataFallsBackAsAWhole()
    {
        var library = new Library { Paths = ["C:\\valid.txt", "relative", ""] };
        var reader = new AsyncClipboardContentReader(library);
        IReadOnlyList<ClipFile> expected = [new("C:\\valid.txt")];
        var fallbacks = 0;
        var result = await reader.ReadFilesAsync(() => { fallbacks++; return Task.FromResult(expected); }, default);
        Assert.Same(expected, result);
        Assert.Equal(1, fallbacks);
    }

    [Fact]
    public async Task LibraryTimeoutUsesWindowsFallback()
    {
        var library = new Library { Error = new TimeoutException("busy") };
        var reader = new AsyncClipboardContentReader(library);
        Assert.Equal("Windows text", await reader.ReadTextAsync(() => Task.FromResult("Windows text"), default));
    }

    [Fact]
    public async Task MissingTextUsesWindowsFallback()
    {
        var reader = new AsyncClipboardContentReader(new Library());
        Assert.Equal("Windows text", await reader.ReadTextAsync(() => Task.FromResult("Windows text"), default));
    }

    [Fact]
    public async Task CancelledLibraryReadNeverStartsFallback()
    {
        using var cancellation = new CancellationTokenSource();
        var library = new Library { BeforeRead = cancellation.Cancel };
        var reader = new AsyncClipboardContentReader(library);
        var fallbacks = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadTextAsync(() => { fallbacks++; return Task.FromResult("wrong"); }, cancellation.Token));
        Assert.Equal(0, fallbacks);
    }

    [Fact]
    public async Task AlreadyCancelledOperationDoesNotReadTheLibrary()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var library = new Library();
        var reader = new AsyncClipboardContentReader(library);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadTextAsync(() => Task.FromResult("wrong"), cancellation.Token));
        Assert.Equal(0, library.TextReads);
    }

    sealed class Library : IAsyncClipboardService
    {
        public string? Text;
        public IEnumerable<string>? Paths;
        public Exception? Error;
        public Action? BeforeRead;
        public int TextReads, FileReads;
        public TimeSpan? Timeout { get; set; }
        public Task<string> GetTextAsync()
        {
            TextReads++; BeforeRead?.Invoke();
            return Error is not null ? Task.FromException<string>(Error) : Task.FromResult(Text!);
        }
        public Task<IEnumerable<string>> GetFileDropListAsync()
        {
            FileReads++;
            return Task.FromResult(Paths!);
        }
        public Task<byte[]> GetAsUnicodeBytesAsync() => throw new NotSupportedException();
        public Task<bool> SetUnicodeBytesAsync(byte[] textBytes) => throw new NotSupportedException();
        public Task<bool> SetTextAsync(string value) => throw new NotSupportedException();
        public Task<bool> SetFileDropListAsync(IEnumerable<string> filePaths) => throw new NotSupportedException();
        public Task<bool> ContainsAsync(ClipboardDataFormat format) => throw new NotSupportedException();
    }
}
