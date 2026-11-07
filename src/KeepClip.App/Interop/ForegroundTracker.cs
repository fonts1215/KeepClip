using KeepClip.Application.Paste;
namespace KeepClip.App.Interop;
internal sealed class ForegroundTracker : IDisposable
{
    readonly ForegroundHistory history = new(new WindowTarget().IsValid);
    readonly NativeMethods.WinEventProc callback;
    readonly nint hook;
    public ForegroundTracker()
    {
        history.Observe(NativeMethods.GetForegroundWindow());
        callback = (_, _, window, _, _, _, _) => history.Observe(window);
        hook = NativeMethods.SetWinEventHook(3, 3, 0, callback, 0, 0, 2);
        if (hook == 0) DiagnosticLog.Write("foreground-hook unavailable");
    }
    public nint Capture() => history.Resolve(NativeMethods.GetForegroundWindow());
    public void Dispose() { if (hook != 0) NativeMethods.UnhookWinEvent(hook); GC.KeepAlive(callback); }
}
