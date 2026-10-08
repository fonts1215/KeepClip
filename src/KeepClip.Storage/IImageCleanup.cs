using KeepClip.Core.History;
namespace KeepClip.Storage;
public interface IImageCleanup { void Clean(IReadOnlyList<ClipEntry> retained); }
