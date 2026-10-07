using ClipShelf.Core;
using ClipShelf.Core.History;
using ClipShelf.Core.Paste;
namespace ClipShelf.Tests;
public class PasteCoordinatorTests
{
    [Theory] [InlineData("closed")] [InlineData("activate")] [InlineData("focus")] [InlineData("modifiers")] [InlineData("input")]
    public async Task UnsafeDestinationFallsBackToCopy(string failure)
    {
        var desktop = new Desktop(failure); var clipboard = new RecordingClipboard();
        var result = await new PasteCoordinator(clipboard, desktop, desktop, () => desktop.Hidden = true).PasteAsync(new(ClipKind.Text, "hello"), 42);
        Assert.Equal(PasteOutcome.CopiedOnly, result.Outcome); Assert.Equal("hello", clipboard.Content!.Text); Assert.True(desktop.Hidden);
        Assert.Equal(failure == "input" ? 1 : 0, desktop.Sent);
    }
    [Fact] public async Task ValidDestinationGetsOnePasteAfterCopyAndHide()
    {
        var desktop = new Desktop(""); var clipboard = new RecordingClipboard();
        var result = await new PasteCoordinator(clipboard, desktop, desktop, () => desktop.Hidden = true).PasteAsync(new(ClipKind.Text, "hello"), 42);
        Assert.Equal(PasteOutcome.InputSent, result.Outcome); Assert.Equal("hello", clipboard.Content!.Text); Assert.Equal(1, desktop.Sent);
    }
    sealed class RecordingClipboard : IClipboardAdapter
    {
        public ClipContent? Content;
        public Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default) => Task.FromResult<ClipboardSnapshot?>(null);
        public Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default) { Content = content; return Task.CompletedTask; }
    }
    sealed class Desktop(string failure) : IWindowTarget, IPasteInput
    {
        public bool Hidden; public int Sent; public nint Capture() => 42;
        public bool IsValid(nint handle) => failure != "closed" && handle == 42;
        public Task<bool> TryActivateAsync(nint handle, CancellationToken ct) => Task.FromResult(Hidden && failure != "activate");
        public bool IsForeground(nint handle) => failure != "focus";
        public Task<bool> WaitForModifiersReleasedAsync(TimeSpan timeout, CancellationToken ct) => Task.FromResult(failure != "modifiers");
        public bool SendPaste() { Sent++; return failure != "input"; }
    }
}
