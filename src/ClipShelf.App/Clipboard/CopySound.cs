using System.Runtime.InteropServices;
namespace ClipShelf.App.Clipboard;
internal static class CopyFeedbackSound
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern bool PlaySound(string sound, nint module, uint flags);
    public static void Play() => PlaySound(Path.Combine(AppContext.BaseDirectory, "Assets", "Copy.wav"), 0, 0x00020000 | 0x0001 | 0x0002);
}
