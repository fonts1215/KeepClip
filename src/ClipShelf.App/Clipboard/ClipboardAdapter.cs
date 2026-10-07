using ClipShelf.App.Interop;
using ClipShelf.Core;
using ClipShelf.Core.History;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;
namespace ClipShelf.App.Clipboard;
internal sealed class ClipboardAdapter : IClipboardAdapter
{
    const string PrivateFormat = "ClipShelf.Owner.v1";
    readonly string owner = Guid.NewGuid().ToString("N");
    public async Task<ClipboardSnapshot?> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); var sequence = NativeMethods.GetClipboardSequenceNumber(); var view = WinClipboard.GetContent();
        if (view.Contains(PrivateFormat) && (await view.GetDataAsync(PrivateFormat)) is string token && token.StartsWith(owner + ":", StringComparison.Ordinal))
            return new(new(ClipKind.Text), sequence, true);
        ClipContent? content = null;
        if (view.Contains(StandardDataFormats.Bitmap))
        {
            var reference = await view.GetBitmapAsync(); using var source = await reference.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(source);
            if ((ulong)decoder.PixelWidth * decoder.PixelHeight > 40_000_000) throw new InvalidDataException("Immagine troppo grande: massimo 40 megapixel.");
            using var output = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync();
            if (output.Size > (ulong)HistoryPolicy.MaxImageBytes) throw new InvalidDataException("Immagine troppo grande: massimo 20 MiB.");
            output.Seek(0); using var reader = new DataReader(output.GetInputStreamAt(0)); await reader.LoadAsync((uint)output.Size);
            var bytes = new byte[checked((int)output.Size)]; reader.ReadBytes(bytes); content = new(ClipKind.Image, Png: bytes);
        }
        else if (view.Contains(StandardDataFormats.Text)) content = new(ClipKind.Text, await view.GetTextAsync());
        ct.ThrowIfCancellationRequested();
        // A delayed producer can change the clipboard while its data is being materialized.
        return content is not null && sequence == NativeMethods.GetClipboardSequenceNumber() ? new(content, sequence) : null;
    }
    public async Task WriteAsync(ClipContent content, Guid operationId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetData(PrivateFormat, owner + ":" + operationId.ToString("N"));
        using var stream = new InMemoryRandomAccessStream();
        if (content.Kind == ClipKind.Text) package.SetText(content.Text ?? "");
        else
        {
            if (content.Png is not { Length: > 0 }) throw new InvalidDataException("Immagine non disponibile.");
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(content.Png); await writer.StoreAsync(); await writer.FlushAsync(); writer.DetachStream(); }
            stream.Seek(0); package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
        }
        WinClipboard.SetContent(package); WinClipboard.Flush();
    }
}
