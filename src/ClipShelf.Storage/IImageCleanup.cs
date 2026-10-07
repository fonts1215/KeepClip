using ClipShelf.Core.History;
namespace ClipShelf.Storage;
public interface IImageCleanup { void Clean(IReadOnlyList<ClipEntry> retained); }
