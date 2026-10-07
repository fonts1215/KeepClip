using ClipShelf.Core.Paste;
namespace ClipShelf.App.Interop;
internal sealed class WindowTarget : IWindowTarget
{
    public nint Capture() { var hwnd = NativeMethods.GetForegroundWindow(); return IsValid(hwnd) ? hwnd : 0; }
    public bool IsValid(nint handle)
    {
        if (handle == 0 || !NativeMethods.IsWindow(handle)) return false;
        NativeMethods.GetWindowThreadProcessId(handle, out var pid); return pid != 0 && pid != Environment.ProcessId;
    }
    public async Task<bool> TryActivateAsync(nint handle, CancellationToken ct)
    {
        if (!IsValid(handle)) return false;
        if (NativeMethods.IsIconic(handle)) NativeMethods.ShowWindow(handle, 9);
        NativeMethods.SetForegroundWindow(handle);
        for (var i = 0; i < 10; i++) { if (IsForeground(handle)) return true; await Task.Delay(20, ct); }
        return false;
    }
    public bool IsForeground(nint handle) => NativeMethods.GetForegroundWindow() == handle;
}
