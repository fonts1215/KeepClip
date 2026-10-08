using System.ComponentModel;
namespace KeepClip.App.Interop;
internal sealed class MessageWindow : IDisposable
{
    readonly NativeMethods.WindowProc proc;
    readonly string className = "KeepClip.Messages." + Guid.NewGuid().ToString("N");
    readonly nint instance = NativeMethods.GetModuleHandle(null);
    public nint Handle { get; }
    public event Action<uint, nuint, nint>? Message;
    public MessageWindow()
    {
        proc = WndProc;
        var cls = new NativeMethods.WindowClass { Proc = proc, ClassName = className, Instance = instance };
        if (NativeMethods.RegisterClass(ref cls) == 0) throw new Win32Exception();
        Handle = NativeMethods.CreateWindowEx(0, className, "KeepClip messages", 0x80000000, 0, 0, 0, 0, 0, 0, instance, 0);
        if (Handle == 0) throw new Win32Exception();
    }
    nint WndProc(nint hwnd, uint msg, nuint wp, nint lp)
    {
        // Exceptions may never cross the unmanaged callback boundary.
        try { Message?.Invoke(msg, wp, lp); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.GetType().Name); }
        return NativeMethods.DefWindowProc(hwnd, msg, wp, lp);
    }
    public void Dispose() { NativeMethods.DestroyWindow(Handle); NativeMethods.UnregisterClass(className, instance); GC.KeepAlive(proc); }
}
