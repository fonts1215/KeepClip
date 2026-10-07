using ClipShelf.Core;
using ClipShelf.Core.History;
namespace ClipShelf.Storage;
public sealed class SqliteHistoryRepository(string directory) : IHistoryRepository
{
    public Task<IReadOnlyList<ClipEntry>> LoadAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task SaveAsync(IReadOnlyList<ClipEntry> entries, CancellationToken ct = default) => throw new NotImplementedException();
}
