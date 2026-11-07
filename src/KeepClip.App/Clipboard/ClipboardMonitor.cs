using System.ComponentModel;
using KeepClip.App.Interop;
using KeepClip.Application;

namespace KeepClip.App.Clipboard;

internal sealed class ClipboardMonitor : IDisposable
{
    readonly MessageWindow window;
    readonly HistoryService history;
    readonly ClipboardSourceTracker sources;
    readonly ClipboardCapturePump capture;
    public event Action<string>? Problem;
    public event Action? Copied;

    public ClipboardMonitor(MessageWindow window, IClipboardAdapter adapter, HistoryService history, ClipboardSourceTracker sources)
    {
        this.window = window; this.history = history; this.sources = sources;
        if (!NativeMethods.AddClipboardFormatListener(window.Handle)) throw new Win32Exception();
        capture = new(history, adapter.ReadAsync);
        capture.Captured += OnCaptured;
        capture.Empty += () => DiagnosticLog.Write("clipboard-unsupported-or-empty");
        capture.Failed += OnFailed;
        DiagnosticLog.Write("listener-registered");
        window.Message += OnMessage;
    }

    void OnMessage(uint message, nuint wp, nint lp)
    {
        if (message != NativeMethods.WM_CLIPBOARDUPDATE) return;
        var sequence = NativeMethods.GetClipboardSequenceNumber();
        DiagnosticLog.Write($"clipboard-notification sequence={sequence} paused={history.IsPaused}");
        if (history.IsPaused) return;
        var owner = SourceAppResolver.ClipboardProcess();
        var foreground = SourceAppResolver.ForegroundProcess();
        if (sequence == NativeMethods.GetClipboardSequenceNumber()) sources.Record(new(sequence, owner, foreground));
        capture.Notify();
    }

    void OnCaptured(ClipboardSnapshot snapshot, CaptureResult result)
    {
        DiagnosticLog.Write($"capture sequence={snapshot.Sequence} kind={snapshot.Content.Kind} completed={result.Captured} own={snapshot.IsOwnWrite} count={history.Entries.Count}");
        if (result.Captured) Copied?.Invoke();
        if (result.Message is not null) Problem?.Invoke(result.Message);
    }

    void OnFailed(Exception ex)
    {
        DiagnosticLog.Write($"capture-error type={ex.GetType().Name} hresult=0x{ex.HResult:X8}");
        Problem?.Invoke(ex is InvalidDataException ? ex.Message : $"Impossibile acquisire gli appunti (0x{ex.HResult:X8}). Riprova a copiare l'elemento.");
    }

    public void Dispose()
    {
        window.Message -= OnMessage;
        NativeMethods.RemoveClipboardFormatListener(window.Handle);
        capture.Dispose();
    }
}
