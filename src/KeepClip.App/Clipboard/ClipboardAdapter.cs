using KeepClip.App.Interop;
using KeepClip.Core;
using KeepClip.Core.History;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Storage;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;
namespace KeepClip.App.Clipboard;
internal sealed class ClipboardAdapter : IClipboardAdapter
{
    readonly ClipboardSourceTracker sourceTracker;
    public ClipboardAdapter(ClipboardSourceTracker sourceTracker) => this.sourceTracker = sourceTracker;
    const string PrivateFormat = "ClipShelf.Owner.v1";
    readonly string owner = Guid.NewGuid().ToString("N");
    readonly AsyncClipboardContentReader reader = new(
        new AsyncWindowsClipboard.WindowsClipboardService(TimeSpan.FromMilliseconds(200)),
        onFallback: ex => DiagnosticLog.Write($"clipboard-library fallback type={ex.GetType().Name} hresult=0x{ex.HResult:X8}"));
    public async Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); var sequence = NativeMethods.GetClipboardSequenceNumber();

        var context = sourceTracker.ForSequence(sequence);
        // Never sample foreground after an asynchronous read: focus may have moved.
        var sourceProcess = context?.OwnerProcess ?? SourceAppResolver.ClipboardProcess();
        var view = WinClipboard.GetContent();
        if (view.Contains(PrivateFormat) && (await view.GetDataAsync(PrivateFormat)) is string token && token.StartsWith(owner + ":", StringComparison.Ordinal))
            return new(new(ClipKind.Text), sequence, true);
        ClipContent? content = null;
        // The service invokes this backend on its dedicated STA dispatcher.
        var nativePng = view.Contains(StandardDataFormats.StorageItems) ? null : NativeClipboardImage.ReadPng();
        if (view.Contains(StandardDataFormats.StorageItems))
        {
            async Task<IReadOnlyList<ClipFile>> WindowsFilesAsync()
            {
                var items = await view.GetStorageItemsAsync();
                if (items.Count > FileReferences.MaxItems) throw new InvalidDataException("Puoi copiare fino a 1000 file o cartelle in una voce.");
                var result = items.Select(item => new ClipFile(item.Path, item.IsOfType(StorageItemTypes.Folder))).ToArray();
                if (!FileReferences.Valid(result)) throw new InvalidDataException("Sono supportati file e cartelle con un percorso disponibile sul dispositivo.");
                return result;
            }
            var files = await reader.ReadFilesAsync(WindowsFilesAsync, ct);
            content = new(ClipKind.Files, Files:files);
        }
        else if (nativePng is not null)
        {
            using var source = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(source.GetOutputStreamAt(0))) { writer.WriteBytes(nativePng); await writer.StoreAsync(); writer.DetachStream(); }
            source.Seek(0); content = new(ClipKind.Image, Png: await NormalizeImageAsync(source));
            DiagnosticLog.Write("clipboard-image native-png");
        }
        else if (view.Contains(StandardDataFormats.Bitmap))
        {
            var reference = await view.GetBitmapAsync(); using var source = await reference.OpenReadAsync();
            content = new(ClipKind.Image, Png: await NormalizeImageAsync(source));
        }
        else if (view.Contains(StandardDataFormats.Text)) content = new(ClipKind.Text, await reader.ReadTextAsync(async () => await view.GetTextAsync(), ct));
        ct.ThrowIfCancellationRequested();
        var origin = content is null ? null : await SourceAppResolver.ResolveClipboardAsync(sourceProcess, context?.ForegroundProcess ?? 0);
        ct.ThrowIfCancellationRequested();
        // A delayed producer can change the clipboard while its data is being materialized.
        if (content is not null && sequence != NativeMethods.GetClipboardSequenceNumber())
        {
            DiagnosticLog.Write("clipboard-sequence-changed-retry");
            throw new System.Runtime.InteropServices.COMException("Appunti cambiati durante la lettura.", unchecked((int)0x800401D0));
        }
        return content is not null ? new(content, sequence, Source: origin) : null;
    }
    static async Task<byte[]> NormalizeImageAsync(IRandomAccessStream source)
    {
            var decoder = await BitmapDecoder.CreateAsync(source);
            if ((ulong)decoder.PixelWidth * decoder.PixelHeight > 40_000_000) throw new InvalidDataException("Immagine troppo grande: massimo 40 megapixel.");
            using var output = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync();
            if (output.Size > (ulong)HistoryPolicy.MaxImageBytes) throw new InvalidDataException("Immagine troppo grande: massimo 20 MiB.");
            output.Seek(0); using var reader = new DataReader(output.GetInputStreamAt(0)); await reader.LoadAsync((uint)output.Size);
            var bytes = new byte[checked((int)output.Size)]; reader.ReadBytes(bytes); return bytes;
    }
    public Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default)
        => WriteIfCurrentAsync(content, operationId, static () => true, ct);

    public async Task WriteIfCurrentAsync(ClipContent content, Guid operationId, Func<bool> isCurrent, CancellationToken ct = default)
    {
        var expectedSequence = NativeMethods.GetClipboardSequenceNumber();
        ct.ThrowIfCancellationRequested(); var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetData(PrivateFormat, owner + ":" + operationId.ToString("N"));
        using var stream = new InMemoryRandomAccessStream();
        if (content.Kind == ClipKind.Text) package.SetText(content.Text ?? "");
        else if (content.Kind == ClipKind.Image)
        {
            if (content.Png is not { Length: > 0 }) throw new InvalidDataException("Immagine non disponibile.");
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(content.Png); await writer.StoreAsync(); await writer.FlushAsync(); writer.DetachStream(); }
            stream.Seek(0); package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
        }
        else if (content.Kind == ClipKind.Files)
        {
            if (!FileReferences.Valid(content.Files)) throw new InvalidDataException("Selezione di file non valida.");
            var items = new List<IStorageItem>();
            foreach (var file in content.Files!)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    IStorageItem item = file.IsDirectory
                        ? await StorageFolder.GetFolderFromPathAsync(file.Path)
                        : await StorageFile.GetFileFromPathAsync(file.Path);
                    items.Add(item);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new IOException("Uno o più file originali non sono più disponibili o accessibili.", ex);
                }
            }
            package.SetStorageItems(items, readOnly: false);
        }
        else throw new InvalidDataException("Tipo di contenuto non supportato.");
        ct.ThrowIfCancellationRequested();
        DiagnosticLog.Write($"clipboard-write begin operation={operationId:N} kind={content.Kind}");
        try
        {
            // Preparing an image or resolving files may await external providers.
            // Do not replace a copy made while that preparation was in progress.
            if (!isCurrent()) throw new ClipboardRequestCancelledException();
            if (NativeMethods.GetClipboardSequenceNumber() != expectedSequence) throw new ClipboardWriteSupersededException();
            DiagnosticLog.Write($"clipboard-write stage=set-start operation={operationId:N}");
            WinClipboard.SetContent(package);
            DiagnosticLog.Write($"clipboard-write stage=set-complete operation={operationId:N} sequence={NativeMethods.GetClipboardSequenceNumber()}");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"clipboard-write stage=set-error operation={operationId:N} type={ex.GetType().Name} hresult=0x{ex.HResult:X8}");
            throw;
        }

        var operationGuard = new ClipboardOperationGuard(owner + ":" + operationId.ToString("N"),
            NativeMethods.GetClipboardSequenceNumber, async token =>
            {
                token.ThrowIfCancellationRequested();
                var view = WinClipboard.GetContent();
                return view.Contains(PrivateFormat) ? await view.GetDataAsync(PrivateFormat) as string : null;
            });
        try
        {
            DiagnosticLog.Write($"clipboard-write stage=flush-start operation={operationId:N}");
            await ClipboardFlushRetry.FlushAsync(WinClipboard.Flush,
                operationGuard.StillOwnedAsync,
                Task.Delay, ct, (attempt, wait) => DiagnosticLog.Write($"clipboard-write stage=flush-busy operation={operationId:N} attempt={attempt} waitMs={wait}"));
            // The private marker identifies a sequence created by Flush. Do not
            // tag the current sequence blindly: another app may have copied meanwhile.
            DiagnosticLog.Write($"clipboard-write stage=flush-complete operation={operationId:N} sequence={NativeMethods.GetClipboardSequenceNumber()}");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"clipboard-write stage=flush-error operation={operationId:N} type={ex.GetType().Name} hresult=0x{ex.HResult:X8} sequence={NativeMethods.GetClipboardSequenceNumber()}");
            throw;
        }
    }
}
