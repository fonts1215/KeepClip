using System.ComponentModel;
using ClipShelf.App.Interop;
using ClipShelf.Core;
namespace ClipShelf.App.Clipboard;
internal sealed class ClipboardMonitor : IDisposable
{
    readonly MessageWindow window; readonly IClipboardAdapter adapter; readonly HistoryService history;
    readonly CancellationTokenSource lifetime = new();
    bool reading, pending;
    public event Action<string>? Problem;
    public ClipboardMonitor(MessageWindow window, IClipboardAdapter adapter, HistoryService history)
    {
        this.window = window; this.adapter = adapter; this.history = history;
        if (!NativeMethods.AddClipboardFormatListener(window.Handle)) throw new Win32Exception();
        window.Message += OnMessage;
    }
    void OnMessage(uint message, nuint wp, nint lp)
    {
        if (message != NativeMethods.WM_CLIPBOARDUPDATE) return;
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
                try
                {
                    var snapshot = await ClipboardReader.ReadWithRetryAsync(adapter, lifetime.Token);
                    if (snapshot is not null) { var result = await history.CaptureAsync(snapshot, lifetime.Token); if (result.Message is not null) Problem?.Invoke(result.Message); }
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                catch (Exception ex) { Problem?.Invoke(ex is InvalidDataException ? ex.Message : "Impossibile acquisire gli appunti. Riprova a copiare l'elemento."); }
            }
        }
        finally { reading = false; }
    }
    public void Dispose() { lifetime.Cancel(); window.Message -= OnMessage; NativeMethods.RemoveClipboardFormatListener(window.Handle); }
}
