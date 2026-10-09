using KeepClip.Core.History;
using KeepClip.Core.Appearance;
namespace KeepClip.Core;
public interface IHistoryRepository
{
    Task<IReadOnlyList<ClipEntry>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(IReadOnlyList<ClipEntry> entries, CancellationToken ct = default);
}
public sealed record Hotkey(uint Modifiers = 6, uint Key = 0x56);
public sealed record AppSettings(Hotkey Hotkey, bool IsPaused = false, bool AutoStart = false, bool CopyOnSelection = false, bool CopySound = false, int CopySoundVolume = 50, AppearanceSettings? Appearance = null)
{
    public static AppSettings Default => new(new Hotkey());
}
