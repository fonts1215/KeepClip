using System.Runtime.InteropServices;
namespace KeepClip.App.Clipboard;
internal static class CopyFeedbackSound
{
    const uint Async = 0x0001, NoDefault = 0x0002, Memory = 0x0004;
    static readonly object gate = new();
    static byte[]? original;
    static GCHandle buffer;
    static int cachedVolume = -1;
    [DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool PlaySound(nint sound, nint module, uint flags);

    public static bool Play(int volume)
    {
        lock (gate)
        {
            volume = Math.Clamp(volume, 0, 100);
            if (volume == 0) { Stop(); return true; }
            try
            {
                if (!buffer.IsAllocated || cachedVolume != volume)
                {
                    original ??= File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "Copy.wav"));
                    var wave = CopySoundWave.WithVolume(original, volume);
                    Stop();
                    // SND_MEMORY | SND_ASYNC requires a stable buffer until playback stops.
                    buffer = GCHandle.Alloc(wave, GCHandleType.Pinned); cachedVolume = volume;
                }
                return PlaySound(buffer.AddrOfPinnedObject(), 0, Memory | Async | NoDefault);
            }
            catch (IOException) { return false; }
            catch (InvalidDataException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    public static void Stop()
    {
        lock (gate)
        {
            if (!buffer.IsAllocated) return;
            PlaySound(0, 0, 0); buffer.Free(); cachedVolume = -1;
        }
    }
}
