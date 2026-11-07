using KeepClip.Storage;
using KeepClip.App.Interop;
using KeepClip.Application;
using KeepClip.Application.History;
using KeepClip.Domain.History;

namespace KeepClip.App.Clipboard;

internal sealed class ClipboardAdapter(ClipboardSourceTracker sourceTracker, Func<nint> ownerWindow) : IClipboardAdapter
{
    readonly NativeClipboard nativeClipboard = new(ownerWindow);
    readonly string owner = Guid.NewGuid().ToString("N");

    public Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(nativeClipboard.Read(owner + ":", GetSourceContext, ClipboardImageCodec.Normalize));
    }

    ClipboardSourceContext GetSourceContext(uint sequence)
        => sourceTracker.ForSequence(sequence) ?? new(sequence, SourceAppResolver.ClipboardProcess(), 0);

    public Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default)
        => WriteIfCurrentAsync(content, operationId, static () => true, ct);

    public async Task WriteIfCurrentAsync(ClipContent content, Guid operationId, Func<bool> isCurrent, CancellationToken ct = default)
    {
        var expectedSequence = NativeMethods.GetClipboardSequenceNumber();
        var token = owner + ":" + operationId.ToString("N");
        ct.ThrowIfCancellationRequested();
        DiagnosticLog.Write($"clipboard-write begin operation={operationId:N} kind={content.Kind} backend=native");
        try
        {
            switch (content.Kind)
            {
                case ClipKind.Text:
                    await nativeClipboard.WriteTextAsync(content.Text ?? "", token, expectedSequence, isCurrent, ct);
                    break;
                case ClipKind.Files:
                    if (!FileReferences.Valid(content.Files)) throw new InvalidDataException("Selezione di file non valida.");
                    if (!await Task.Run(() => StoredClipContentLoader.FilesAvailable(content.Files!), ct))
                        throw new IOException("Uno o più file originali non sono più disponibili o accessibili.");
                    await nativeClipboard.WriteAsync(content.Files!, token, expectedSequence, isCurrent, ct);
                    break;
                case ClipKind.Image:
                    if (content.Png is not { Length: > 0 } png) throw new InvalidDataException("Immagine non disponibile.");
                    var dib = await Task.Run(() => ClipboardImageCodec.ToDibV5(png), ct);
                    await nativeClipboard.WriteImageAsync(png, dib, token, expectedSequence, isCurrent, ct);
                    break;
                default:
                    throw new InvalidDataException("Tipo di contenuto non supportato.");
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"clipboard-write stage=native-error operation={operationId:N} type={ex.GetType().Name} hresult=0x{ex.HResult:X8}");
            throw;
        }
        DiagnosticLog.Write($"clipboard-write stage=native-complete operation={operationId:N} kind={content.Kind} sequence={NativeMethods.GetClipboardSequenceNumber()}");
    }
}
