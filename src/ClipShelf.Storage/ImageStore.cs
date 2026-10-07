namespace ClipShelf.Storage;
public sealed class ImageStore(string directory)
{
    public Task<string> SaveAsync(Guid id, byte[] png, CancellationToken ct = default) => throw new NotImplementedException();
}
