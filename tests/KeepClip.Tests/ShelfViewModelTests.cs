using KeepClip.Application;
using KeepClip.Domain.History;
using KeepClip.App.Shelf;
namespace KeepClip.Tests;
public class ShelfViewModelTests
{
    static ClipEntry Text(string text, bool pinned = false) => new(Guid.NewGuid(), ClipKind.Text, text, DateTimeOffset.UtcNow, pinned, text, null, text.Length);
    [Fact] public void UnicodeSearchIsCaseInsensitive()
    {
        var vm = new ShelfViewModel(); vm.Refresh([Text("CAFfÈ"), Text("other")]); vm.Query = "caffè"; Assert.Equal("CAFfÈ", Assert.Single(vm.Entries).Text);
    }
    [Fact] public void PinnedFilterOnlyShowsPinnedEntries()
    {
        var vm = new ShelfViewModel(); vm.Refresh([Text("keep", true), Text("other")]); vm.ShowPinnedOnly = true; Assert.Equal("keep", Assert.Single(vm.Entries).Text);
    }
    [Fact] public void FilteringReplacesInvalidSelection()
    {
        var a = Text("A"); var b = Text("B"); var vm = new ShelfViewModel(); vm.Refresh([a, b]); vm.SelectedEntry = a; vm.Query = "B"; Assert.Equal(b.Id, vm.SelectedEntry!.Id);
        vm.Query = "missing"; Assert.Null(vm.SelectedEntry);
    }
    [Fact] public async Task ClearHistoryCanPreserveOrIncludePinned()
    {
        var service = new HistoryService(new ClipboardCaptureTests.MemoryRepository(), (id, bytes, ct) => Task.FromResult("image"));
        await service.CaptureAsync(new(new(ClipKind.Text, "keep"), 1)); await service.TogglePinAsync(service.Entries[0].Id);
        await service.CaptureAsync(new(new(ClipKind.Text, "remove"), 2)); await service.ClearAsync(false); Assert.Equal("keep", Assert.Single(service.Entries).Text);
        await service.ClearAsync(true); Assert.Empty(service.Entries);
    }
    [Fact] public async Task FailedMutationDoesNotUpdateHistory()
    {
        var repository = new ClipboardCaptureTests.MemoryRepository(); var service = new HistoryService(repository, (id, bytes, ct) => Task.FromResult("image"));
        await service.CaptureAsync(new(new(ClipKind.Text, "saved"), 1)); repository.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => service.DeleteAsync(service.Entries[0].Id)); Assert.Single(service.Entries);
    }
}
