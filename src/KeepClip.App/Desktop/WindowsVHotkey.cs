using System.Runtime.InteropServices;
using KeepClip.App.Interop;

namespace KeepClip.App.Desktop;

internal sealed class WindowsVHotkey : IDisposable
{
    const int LowLevelKeyboardHook = 13;
    const int InvalidHookHandle = 1404;
    const int V = 0x56, LeftWindows = 0x5B, RightWindows = 0x5C;
    const int Shift = 0x10, Control = 0x11, Alt = 0x12;
    const uint Injected = 0x10;
    static readonly int InputSize = Marshal.SizeOf<NativeMethods.Input>();
    // A dummy key-up keeps Start from opening when the shell sees Win released
    // after we consume V. This is also used by the PowerToys centralized hook.
    static readonly NativeMethods.Input[] StartMenuMask =
    [
        new() { Type = 1, Data = new() { Keyboard = new() { Vk = 0xFF, Flags = 2 } } }
    ];

    readonly nint window;
    readonly NativeMethods.KeyboardHookProc callback;
    readonly Func<bool> postShortcut;
    readonly WindowsVGesture gesture = new();
    nint hook;
    nuint generation;
    bool enabled;

    public WindowsVHotkey(nint window)
    {
        this.window = window;
        callback = OnKeyboard;
        postShortcut = PostShortcut;
    }

    public bool SetEnabled(bool value)
    {
        if (enabled == value) return true;
        if (!value)
        {
            enabled = false;
            generation++;
            RemoveHook();
            return true;
        }

        RemoveHook();
        if (hook != 0) return false;
        gesture.Reset(vAlreadyDown: IsDown(V));
        hook = NativeMethods.SetWindowsHookEx(LowLevelKeyboardHook, callback, NativeMethods.GetModuleHandle(null), 0);
        if (hook == 0)
        {
            DiagnosticLog.Write($"windows-v-hook error={Marshal.GetLastWin32Error()}");
            return false;
        }
        generation++;
        enabled = true;
        return true;
    }

    public bool IsCurrent(nuint token) => enabled && token == generation;

    nint OnKeyboard(int code, nuint message, nint data)
    {
        if (enabled && code == 0 && message is 0x0100 or 0x0101 or 0x0104 or 0x0105)
        {
            try
            {
                var key = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(data);
                if (key.VirtualKey == V && (key.Flags & Injected) == 0)
                {
                    var down = message is 0x0100 or 0x0104;
                    var windowsOnly = down && (IsDown(LeftWindows) || IsDown(RightWindows)) &&
                        !IsDown(Shift) && !IsDown(Control) && !IsDown(Alt);
                    if (gesture.Handle(down, windowsOnly, postShortcut)) return 1;
                }
            }
            catch { /* Never interrupt the keyboard chain if the callback fails. */ }
        }
        return NativeMethods.CallNextHookEx(hook, code, message, data);
    }

    bool PostShortcut() =>
        NativeMethods.SendInput(1, StartMenuMask, InputSize) == 1 &&
        NativeMethods.PostMessage(window, NativeMethods.WM_KEEPCLIP_WINDOWS_V, generation, 0);

    static bool IsDown(int key) => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0;

    void RemoveHook()
    {
        if (hook == 0) return;
        if (NativeMethods.UnhookWindowsHookEx(hook))
        {
            hook = 0;
            return;
        }
        var error = Marshal.GetLastWin32Error();
        if (error == InvalidHookHandle) hook = 0;
        else DiagnosticLog.Write($"windows-v-unhook error={error}");
    }

    public void Dispose()
    {
        SetEnabled(false);
        RemoveHook();
        GC.KeepAlive(callback);
    }
}
