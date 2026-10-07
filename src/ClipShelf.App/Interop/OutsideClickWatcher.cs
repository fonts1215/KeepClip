using System.Runtime.InteropServices;
namespace ClipShelf.App.Interop;
// Observe only button presses while the shelf is visible. Never consume input.
internal sealed class OutsideClickWatcher(nint window, Func<bool> watch, Action outside) : IDisposable
{
    NativeMethods.MouseHookProc? callback;
    nint hook;
    public void Start()
    {
        if (hook != 0) return;
        callback ??= OnMouse;
        hook = NativeMethods.SetWindowsHookEx(14, callback, NativeMethods.GetModuleHandle(null), 0);
        if (hook == 0) DiagnosticLog.Write($"outside-click-hook error={Marshal.GetLastWin32Error()}");
    }
    nint OnMouse(int code, nuint message, nint data)
    {
        try
        {
            if (code >= 0 && message is 0x0201 or 0x0204 or 0x0207 or 0x020B && watch())
            {
                var point = Marshal.PtrToStructure<NativeMethods.MouseHookData>(data).Point;
                if (NativeMethods.GetWindowRect(window, out var rect) &&
                    (point.X < rect.Left || point.X >= rect.Right || point.Y < rect.Top || point.Y >= rect.Bottom)) outside();
            }
        }
        catch { /* Exceptions must not cross the native hook boundary. */ }
        return NativeMethods.CallNextHookEx(hook, code, message, data);
    }
    public void Stop() { if (hook != 0) { NativeMethods.UnhookWindowsHookEx(hook); hook = 0; } GC.KeepAlive(callback); }
    public void Dispose() => Stop();
}
