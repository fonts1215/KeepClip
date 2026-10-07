using System.Runtime.InteropServices;
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
    sealed record Request(NativeMethods.Point Point, nint Window, uint Sequence, long Generation, long Version, CancellationToken Token);
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
                            var request = new Request(mouse.Point, window, downSequence, history.CaptureGeneration, ++version, lifetime.Token);
                            dispatcher.TryEnqueue(() => { if (Current(request)) { pending = request; if (!reading) ReadPending(); } });
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
                    await Task.Delay(100, request.Token);
                    if (!Current(request) || NativeMethods.GetClipboardSequenceNumber() != request.Sequence) continue;
                    NativeMethods.GetWindowThreadProcessId(request.Window, out var process);
                    SelectedText? text;
                    try { text = await Task.Run(() => SelectedTextReader.Read(request.Point, process), request.Token); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { DiagnosticLog.Write($"selection-reader-unavailable type={ex.GetType().Name}"); continue; }
                    if (text is null || !Current(request) || NativeMethods.GetClipboardSequenceNumber() != request.Sequence) continue;
                    var origin = await SourceAppResolver.ResolveAsync(process);
                    if (!Current(request) || NativeMethods.GetClipboardSequenceNumber() != request.Sequence) continue;
                    var saved = await copy.CopyAsync(text with { Source = origin }, () => Current(request), request.Token);
                    DiagnosticLog.Write($"selection-copy captured={saved}");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    DiagnosticLog.Write($"selection-copy-error type={ex.GetType().Name}");
                    Problem?.Invoke("Impossibile completare la copia alla selezione. Riprova con Ctrl+C.");
                }
            }
        }
        finally { reading = false; }
    }
    public void Dispose() => Stop();
}
