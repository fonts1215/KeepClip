using ClipShelf.Core;
using ClipShelf.Core.History;
using ClipShelf.Storage;
namespace ClipShelf.Tests;
public sealed class RecoveryTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "ClipShelf.Recovery", Guid.NewGuid().ToString("N"));
    [Fact] public async Task FailedImageBackupLeavesOriginalDatabaseAndImagesIntact()
    {
        Directory.CreateDirectory(Path.Combine(root, "images")); var db = Path.Combine(root, "history.db"); var image = Path.Combine(root, "images", "locked.png");
        await File.WriteAllTextAsync(db, "original-db"); await File.WriteAllBytesAsync(image, [1, 2, 3]);
        using (var held = File.Open(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => new HistoryRecovery(root).BackupAsync());
        Assert.Equal("original-db", await File.ReadAllTextAsync(db)); Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(image));
    }
    [Fact] public async Task SuccessfulBackupPreservesCompleteOriginalBeforeReset()
    {
        Directory.CreateDirectory(Path.Combine(root, "images")); await File.WriteAllTextAsync(Path.Combine(root, "history.db"), "database"); await File.WriteAllBytesAsync(Path.Combine(root, "images", "one.png"), [4, 5]);
        var backup = await new HistoryRecovery(root).BackupAsync();
        Assert.Equal("database", await File.ReadAllTextAsync(Path.Combine(backup, "history.db"))); Assert.Equal(new byte[] { 4, 5 }, await File.ReadAllBytesAsync(Path.Combine(backup, "images", "one.png")));
        Assert.True(File.Exists(Path.Combine(root, "history.db"))); Assert.True(File.Exists(Path.Combine(root, "images", "one.png")));
    }
    [Fact] public async Task CleanupFailureAfterCommitDoesNotLeaveHistoryStale()
    {
        var repository = new SqliteHistoryRepository(root, new DeniedCleanup()); var service = new HistoryService(repository, (id, bytes, ct) => Task.FromResult("unused"));
        await service.CaptureAsync(new(new(ClipKind.Text, "committed"), 1)); Assert.Equal("committed", Assert.Single(service.Entries).Text);
        Assert.Equal("committed", Assert.Single(await new SqliteHistoryRepository(root).LoadAsync()).Text);
    }
    sealed class DeniedCleanup : IImageCleanup { public void Clean(IReadOnlyList<ClipEntry> retained) => throw new UnauthorizedAccessException("enumeration denied"); }
    [Fact] public async Task CorruptDatabaseCanBeResetOnlyAfterCompleteBackup()
    {
        Directory.CreateDirectory(root); await File.WriteAllBytesAsync(Path.Combine(root, "history.db"), [1, 2, 3, 4]);
        var service = new HistoryService(new SqliteHistoryRepository(root), (id, bytes, ct) => Task.FromResult("unused"));
        await service.ResetWithBackupAsync(new HistoryRecovery(root).PrepareResetAsync);
        Assert.Empty(await new SqliteHistoryRepository(root).LoadAsync());
        var backup = Assert.Single(Directory.GetDirectories(root, "backup-*"));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(Path.Combine(backup, "history.db")));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
