using KeepClip.App.Interop;
using KeepClip.Core;
namespace KeepClip.App.Desktop;
internal sealed class HotkeyService : IDisposable
{
    readonly MessageWindow window; int registeredId;
    public Hotkey? Current { get; private set; }
    public event Action? Invoked;
    public HotkeyService(MessageWindow window) { this.window = window; window.Message += OnMessage; }
    public bool TryRegister(Hotkey hotkey)
    {
        if (hotkey == Current) return true;
        if (hotkey.Modifiers == 0 || hotkey.Key is < 0x41 or > 0x5A) return false;
        var nextId = registeredId == 1 ? 2 : 1;
        if (!NativeMethods.RegisterHotKey(window.Handle, nextId, hotkey.Modifiers | 0x4000, hotkey.Key)) return false;
        if (registeredId != 0) NativeMethods.UnregisterHotKey(window.Handle, registeredId);
        registeredId = nextId; Current = hotkey; return true;
    }
    void OnMessage(uint message, nuint wp, nint lp) { if (message == NativeMethods.WM_HOTKEY && (int)wp == registeredId) Invoked?.Invoke(); }
    public void Dispose() { window.Message -= OnMessage; if (registeredId != 0) NativeMethods.UnregisterHotKey(window.Handle, registeredId); }
}
