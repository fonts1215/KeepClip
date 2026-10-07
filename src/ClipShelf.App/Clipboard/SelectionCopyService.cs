using System.Runtime.InteropServices;
using System.Diagnostics;
using ClipShelf.App.Interop;
using ClipShelf.Core;
using ClipShelf.Core.Selection;
using Microsoft.UI.Dispatching;
namespace ClipShelf.App.Clipboard;
internal sealed class SelectionCopyService(DispatcherQueue dispatcher, HistoryService history, SelectionCopyCoordinator copy, Func<bool> enabled) : IDisposable
{
    NativeMethods.MouseHookProc? mouseCallback;
    nint hook;
    long version, lastDownTime;
    NativeMethods.Point down, previousDown;
    uint downSequence;
    bool pressed, doubleClick, reading;
    Request? pending;
    CancellationTokenSource? lifetime;
    public event Action<string>? Problem;
    sealed record Request(NativeMethods.Point Point, NativeMethods.Point Start, nint Window, uint Sequence, long Generation, long Version, CancellationToken Token);
    public bool Start()
    {
        if (hook != 0) return true;
        mouseCallback ??= OnMouse;
        lifetime = new();
        hook = NativeMethods.SetWindowsHookEx(14, mouseCallback, NativeMethods.GetModuleHandle(null), 0);
        if (hook != 0) return true;
        lifetime.Dispose(); lifetime = null; DiagnosticLog.Write($"selection-hook error={Marshal.GetLastWin32Error()}"); return false;
    }
    public void Stop()
    {
        version++; pressed = false; lastDownTime = 0; pending = null; lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
        if (hook != 0) { NativeMethods.UnhookWindowsHookEx(hook); hook = 0; }
        GC.KeepAlive(mouseCallback);
    }
    nint OnMouse(int code, nuint message, nint data)
    {
        try
        {
            if (code >= 0 && message is 0x0201 or 0x0202 && enabled())
            {
                var mouse = Marshal.PtrToStructure<NativeMethods.MouseHookData>(data);
                if ((mouse.Flags & 1) == 0)
                {
                    if (message == 0x0201)
                    {
                        version++; pending = null; down = mouse.Point; downSequence = NativeMethods.GetClipboardSequenceNumber(); pressed = true;
                        var now = Environment.TickCount64;
                        doubleClick = lastDownTime != 0 && now - lastDownTime <= NativeMethods.GetDoubleClickTime() && Math.Abs(down.X - previousDown.X) <= 4 && Math.Abs(down.Y - previousDown.Y) <= 4;
                        lastDownTime = now; previousDown = down;
                    }
                    else if (pressed)
                    {
                        pressed = false;
                        var drag = Math.Abs(mouse.Point.X - down.X) >= 3 || Math.Abs(mouse.Point.Y - down.Y) >= 3;
                        var shift = (NativeMethods.GetAsyncKeyState(0x10) & 0x8000) != 0;
                        var modifiers = new[] { 0x11, 0x12, 0x5B, 0x5C }.Any(key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0);
                        var window = NativeMethods.GetForegroundWindow(); NativeMethods.GetWindowThreadProcessId(window, out var process);
                        if ((drag || doubleClick || shift) && !modifiers && process != 0 && process != Environment.ProcessId && lifetime is not null)
                        {
                            var request = new Request(mouse.Point, down, window, downSequence, history.CaptureGeneration, ++version, lifetime.Token);
                            dispatcher.TryEnqueue(() =>
                            {
                                if (!Current(request)) { DiagnosticLog.Write($"selection-discard request={request.Version} stage=queue reason={InvalidReason(request)}"); return; }
                                DiagnosticLog.Write($"selection-gesture request={request.Version} gesture={(drag ? "drag" : doubleClick ? "double-click" : "shift-click")}");
                                pending = request; if (!reading) ReadPending();
                            });
                        }
                    }
                }
            }
        }
        catch { /* Never let an exception cross the native callback boundary. */ }
        return NativeMethods.CallNextHookEx(hook, code, message, data);
    }
    bool Current(Request request) => !request.Token.IsCancellationRequested && request.Version == version && enabled() &&
        !history.IsPaused && history.CaptureGeneration == request.Generation && NativeMethods.GetForegroundWindow() == request.Window;
    string InvalidReason(Request request, bool checkClipboard = false)
    {
        if (request.Token.IsCancellationRequested) return "stopped";
        if (request.Version != version) return "new-gesture";
        if (!enabled()) return "disabled-or-not-ready";
        if (history.IsPaused || history.CaptureGeneration != request.Generation) return "capture-state-changed";
        if (NativeMethods.GetForegroundWindow() != request.Window) return "foreground-changed";
        if (checkClipboard && NativeMethods.GetClipboardSequenceNumber() != request.Sequence) return "clipboard-changed";
        return "none";
    }
    static string ProcessLabel(uint process)
    {
        try { using var app = Process.GetProcessById(checked((int)process)); return app.ProcessName.Replace((char)13, (char)95).Replace((char)10, (char)95); }
        catch { return "unavailable"; }
    }
    async void ReadPending()
    {
        reading = true;
        try
        {
            while (pending is { } request)
            {
                pending = null;
                try
                {
                    NativeMethods.GetWindowThreadProcessId(request.Window, out var process);
                    DiagnosticLog.Write($"selection-begin request={request.Version} process={process} app={ProcessLabel(process)} sequence={request.Sequence}");
                    var attempt = 0; var lastReason = "";
                    bool CanRead()
                    {
                        var reason = InvalidReason(request, checkClipboard: true);
                        if (reason != "none" && reason != lastReason) DiagnosticLog.Write($"selection-discard request={request.Version} stage=read reason={reason}");
                        lastReason = reason; return reason == "none";
                    }
                    var text = await SelectionReadRetry.ReadAsync(async token =>
                    {
                        attempt++;
                        try { return await Task.Run(() => SelectedTextReader.Read(request.Point, request.Start, process, detail => DiagnosticLog.Write($"selection-probe request={request.Version} attempt={attempt} {detail}")), token); }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { DiagnosticLog.Write($"selection-reader-unavailable request={request.Version} attempt={attempt} type={ex.GetType().Name} hresult=0x{ex.HResult:X8}"); return null; }
                    }, CanRead,
                    (ms, token) => Task.Delay(ms, token), request.Token);
                    DiagnosticLog.Write($"selection-read request={request.Version} attempts={attempt} found={text is not null} password={text?.IsPassword == true} own={text?.IsOwnApp == true}");
                    if (text is null || text.IsPassword || text.IsOwnApp || !CanRead()) continue;
                    var origin = await SourceAppResolver.ResolveAsync(process);
                    if (!CanRead()) continue;
                    DiagnosticLog.Write($"selection-copy-begin request={request.Version}");
                    var saved = await copy.CopyAsync(text with { Source = origin }, () => Current(request), request.Token);
                    DiagnosticLog.Write($"selection-copy request={request.Version} captured={saved} reason={InvalidReason(request)}");
                }
                catch (ClipboardWriteSupersededException) { DiagnosticLog.Write($"selection-discard request={request.Version} stage=flush reason=clipboard-changed"); }
                catch (OperationCanceledException) { DiagnosticLog.Write($"selection-discard request={request.Version} stage=cancel reason={InvalidReason(request)}"); }
                catch (Exception ex)
                {
                    DiagnosticLog.Write($"selection-copy-error request={request.Version} type={ex.GetType().Name} hresult=0x{ex.HResult:X8}");
                    Problem?.Invoke("Impossibile completare la copia alla selezione. Riprova con Ctrl+C.");
                }
            }
        }
        finally { reading = false; }
    }
    public void Dispose() => Stop();
}
