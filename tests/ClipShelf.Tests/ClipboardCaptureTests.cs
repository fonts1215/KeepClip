using ClipShelf.Core;
using ClipShelf.Core.History;
namespace ClipShelf.Tests;
public class ClipboardCaptureTests
{
    internal sealed class MemoryRepository : IHistoryRepository
    {
        public IReadOnlyList<ClipEntry> Stored = []; public bool FailSave;
        public Task<IReadOnlyList<ClipEntry>> LoadAsync(CancellationToken ct = default) => Task.FromResult(Stored);
        public Task SaveAsync(IReadOnlyList<ClipEntry> entries, CancellationToken ct = default) { if (FailSave) throw new IOException("disk full"); Stored = entries.ToArray(); return Task.CompletedTask; }
    }
    static HistoryService Service(MemoryRepository? repository = null) => new(repository ?? new(), (id, bytes, ct) => Task.FromResult(id + ".png"));
    [Fact] public async Task LateOlderReadCannotReplaceLatestCopy()
    {
        var service = Service(); await service.CaptureAsync(new(new(ClipKind.Text, "B"), 102)); await service.CaptureAsync(new(new(ClipKind.Text, "A"), 101));
        Assert.Equal("B", Assert.Single(service.Entries).Text);
    }
    [Fact] public async Task OwnWriteAndPausedEventsAreIgnored()
    {
        var service = Service(); await service.CaptureAsync(new(new(ClipKind.Text, "own"), 1, true));
        service.SetPaused(true); await service.CaptureAsync(new(new(ClipKind.Text, "paused"), 2));
        Assert.Empty(service.Entries); service.SetPaused(false); await service.CaptureAsync(new(new(ClipKind.Text, "visible"), 3));
        Assert.Equal("visible", Assert.Single(service.Entries).Text);
    }
    [Fact] public async Task EmptyAndUnsupportedAreIgnored()
    {
        var service = Service(); await service.CaptureAsync(new(new(ClipKind.Text, ""), 1)); await service.CaptureAsync(new(new(ClipKind.Image), 2)); Assert.Empty(service.Entries);
    }
    [Fact] public async Task DuplicateDoesNotCreateExtraEntry()
    {
        var service = Service(); await service.CaptureAsync(new(new(ClipKind.Text, "same"), 1)); var id = service.Entries[0].Id;
        await service.TogglePinAsync(id); await service.CaptureAsync(new(new(ClipKind.Text, "same"), 2));
        Assert.Equal(id, Assert.Single(service.Entries).Id); Assert.True(service.Entries[0].IsPinned);
    }
    [Fact] public async Task FailedSaveLeavesVisibleHistoryUnchanged()
    {
        var repository = new MemoryRepository(); var service = Service(repository); await service.CaptureAsync(new(new(ClipKind.Text, "saved"), 1));
        repository.FailSave = true; await Assert.ThrowsAsync<IOException>(() => service.CaptureAsync(new(new(ClipKind.Text, "lost"), 2)));
        Assert.Equal("saved", Assert.Single(service.Entries).Text);
    }
    [Fact] public async Task BusyClipboardStopsAfterThreeAttempts()
    {
        var adapter = new BusyAdapter(); await Assert.ThrowsAsync<System.Runtime.InteropServices.COMException>(() => ClipboardReader.ReadWithRetryAsync(adapter)); Assert.Equal(3, adapter.Reads);
    }
    sealed class BusyAdapter : IClipboardAdapter
    {
        public int Reads;
        public Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default) { Reads++; throw new System.Runtime.InteropServices.COMException("busy"); }
        public Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
