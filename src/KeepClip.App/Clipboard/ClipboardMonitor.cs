using System.ComponentModel;
using KeepClip.App.Interop;
using KeepClip.Core;
namespace KeepClip.App.Clipboard;
internal sealed class ClipboardMonitor : IDisposable
{
    readonly MessageWindow window; readonly IClipboardAdapter adapter; readonly HistoryService history;
    readonly CancellationTokenSource lifetime = new();
    bool reading, pending;
    public event Action<string>? Problem;
    public event Action? Copied;
    public ClipboardMonitor(MessageWindow window, IClipboardAdapter adapter, HistoryService history)
    {
        this.window = window; this.adapter = adapter; this.history = history;
        if (!NativeMethods.AddClipboardFormatListener(window.Handle)) throw new Win32Exception();
        DiagnosticLog.Write("listener-registered");
        window.Message += OnMessage;
    }
    void OnMessage(uint message, nuint wp, nint lp)
    {
        if (message != NativeMethods.WM_CLIPBOARDUPDATE) return;
        DiagnosticLog.Write($"clipboard-notification paused={history.IsPaused}");
        if (history.IsPaused) return;
        pending = true; if (!reading) ReadPending();
    }
    async void ReadPending()
    {
        reading = true;
        try
        {
            while (pending && !lifetime.IsCancellationRequested)
            {
                pending = false;
                var generation = history.CaptureGeneration;
                if (history.IsPaused) continue;
                try
                {
                    var snapshot = await ClipboardReader.ReadWithRetryAsync(adapter, lifetime.Token);
                    if (snapshot is not null)
                    {
                        var result = await history.CaptureAsync(snapshot with { CaptureGeneration = generation }, lifetime.Token);
                        DiagnosticLog.Write($"capture completed={result.Captured} own={snapshot.IsOwnWrite} count={history.Entries.Count}");
                        if (result.Captured) Copied?.Invoke();
                        if (result.Message is not null) Problem?.Invoke(result.Message);
                    }
                    else DiagnosticLog.Write("clipboard-unsupported-or-empty");
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    DiagnosticLog.Write($"capture-error type={ex.GetType().Name} hresult=0x{ex.HResult:X8}");
                    Problem?.Invoke(ex is InvalidDataException ? ex.Message : $"Impossibile acquisire gli appunti (0x{ex.HResult:X8}). Riprova a copiare l'elemento.");
                }
            }
        }
        finally { reading = false; }
    }
    public void Dispose() { lifetime.Cancel(); window.Message -= OnMessage; NativeMethods.RemoveClipboardFormatListener(window.Handle); }
}
