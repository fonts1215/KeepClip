using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using KeepClip.App.Interop;
using KeepClip.Application;
using KeepClip.Application.History;
using KeepClip.Domain.History;

namespace KeepClip.App.Clipboard;

internal interface INativeClipboardApi
{
    uint Sequence { get; }
    uint RegisterFormat(string name);
    bool Contains(uint format);
    bool Open(nint owner);
    void Close();
    bool Empty();
    nint Allocate(byte[] bytes);
    void Free(nint handle);
    bool Publish(uint format, nint handle);
    byte[]? Read(uint format, int maxBytes);
    IReadOnlyList<string> ReadFiles();
}

internal sealed class NativeClipboard
{
    internal const string OwnerFormat = "KeepClip.Native.Owner.v1";
    const uint UnicodeTextFormat = 13; // CF_UNICODETEXT
    const uint FileDropFormat = 15; // CF_HDROP
    const uint DibFormat = 8; // CF_DIB
    const uint DibV5Format = 17; // CF_DIBV5
    const int MaxDibBytes = 162 * 1024 * 1024;
    readonly Func<nint> ownerWindow;
    readonly INativeClipboardApi api;
    readonly uint ownerFormat, effectFormat, pngFormat;
    public NativeClipboard(Func<nint> ownerWindow, INativeClipboardApi? api = null)
    {
        this.ownerWindow = ownerWindow; this.api = api ?? new WindowsClipboardApi();
        ownerFormat = this.api.RegisterFormat(OwnerFormat); effectFormat = this.api.RegisterFormat("Preferred DropEffect");
        pngFormat = this.api.RegisterFormat("PNG");
        if (ownerFormat == 0 || effectFormat == 0 || pngFormat == 0) throw new Win32Exception();
    }
    public ClipboardSnapshot? Read(string ownerPrefix, Func<uint, ClipboardSourceContext>? source = null,
        Func<byte[], bool, byte[]>? normalizeImage = null, Func<string, bool>? isDirectory = null)
    {
        if (!api.Open(0)) throw Busy();
        try
        {
            if (api.Contains(ownerFormat) && DecodeUnicode(api.Read(ownerFormat, 2048))?.StartsWith(ownerPrefix, StringComparison.Ordinal) == true)
                return new(new(ClipKind.Text), api.Sequence, IsOwnWrite: true);

            var context = source?.Invoke(api.Sequence);
            if (api.Contains(FileDropFormat))
            {
                var paths = api.ReadFiles();
                return new(new(ClipKind.Files), api.Sequence, SourceContext: context,
                    PrepareContent: () => PrepareFiles(paths, isDirectory));
            }

            if (normalizeImage is not null)
            {
                foreach (var format in new[] { pngFormat, DibV5Format, DibFormat })
                {
                    if (!api.Contains(format)) continue;
                    var bytes = api.Read(format, format == pngFormat ? (int)HistoryPolicy.MaxImageBytes : MaxDibBytes);
                    if (bytes is not null)
                        return new(new(ClipKind.Image), api.Sequence, SourceContext: context,
                            PrepareContent: () => new(ClipKind.Image, Png: normalizeImage(bytes, format != pngFormat)));
                }
            }

            if (!api.Contains(UnicodeTextFormat)) return null;
            // A valid UTF-8 entry can require up to twice as many UTF-16 bytes.
            var text = DecodeUnicode(api.Read(UnicodeTextFormat, (int)(2 * HistoryPolicy.MaxTotalBytes + 2)));
            return text is null ? null : new(new(ClipKind.Text, text), api.Sequence, SourceContext: context);
        }
        finally { api.Close(); }
    }

    static ClipContent PrepareFiles(IReadOnlyList<string> paths, Func<string, bool>? isDirectory)
    {
        var files = paths.Select(path => new ClipFile(path)).ToArray();
        if (!FileReferences.Valid(files))
            throw new InvalidDataException("Sono supportati fino a 1000 file o cartelle con un percorso disponibile sul dispositivo.");
        return new(ClipKind.Files, Files: files.Select(file => file with
        {
            IsDirectory = isDirectory?.Invoke(file.Path) ?? (File.GetAttributes(file.Path) & FileAttributes.Directory) != 0
        }).ToArray());
    }

    static string? DecodeUnicode(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 2) return null;
        // GlobalSize can include padding beyond the terminator.
        for (var i = 0; i + 1 < bytes.Length; i += 2)
            if (bytes[i] == 0 && bytes[i + 1] == 0) return Encoding.Unicode.GetString(bytes, 0, i);
        return null;
    }

    public Task WriteTextAsync(string text, string token, uint expectedSequence, Func<bool> isCurrent, CancellationToken ct)
        => PublishAsync(token, [(UnicodeTextFormat, Encoding.Unicode.GetBytes(text + "\0"))], expectedSequence, isCurrent, ct);

    public Task WriteImageAsync(byte[] png, byte[] dib, string token, uint expectedSequence, Func<bool> isCurrent, CancellationToken ct)
        => PublishAsync(token, [(pngFormat, png), (DibV5Format, dib)], expectedSequence, isCurrent, ct);

    public async Task WriteAsync(IReadOnlyList<ClipFile> files, string token, uint expectedSequence, Func<bool> isCurrent, CancellationToken ct)
    {
        if (!FileReferences.Valid(files)) throw new InvalidDataException("Selezione di file non valida.");
        // DROPFILES has a fixed 20-byte header followed by UTF-16 MULTI_SZ.
        var paths = Encoding.Unicode.GetBytes(string.Join('\0', files.Select(file => Path.GetFullPath(file.Path))) + "\0\0");
        var dropFiles = new byte[checked(20 + paths.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(dropFiles, 20);
        BinaryPrimitives.WriteUInt32LittleEndian(dropFiles.AsSpan(16), 1); // fWide
        paths.CopyTo(dropFiles, 20);
        await PublishAsync(token, [(effectFormat, [1, 0, 0, 0]), (FileDropFormat, dropFiles)], expectedSequence, isCurrent, ct);
    }

    async Task PublishAsync(string token, (uint Format, byte[] Bytes)[] content, uint expectedSequence, Func<bool> isCurrent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 1023 || token.Contains('\0')) throw new ArgumentException("Identificatore copia non valido.", nameof(token));
        var window = ownerWindow();
        if (window == 0) throw new InvalidOperationException("Finestra proprietaria degli appunti non disponibile.");
        (uint Format, byte[] Bytes)[] payloads = [(ownerFormat, Encoding.Unicode.GetBytes(token + "\0")), .. content];
        var handles = new nint[payloads.Length];
        try
        {
            // Allocate everything before EmptyClipboard, preserving old data on OOM.
            for (var i = 0; i < payloads.Length; i++) { ct.ThrowIfCancellationRequested(); handles[i] = api.Allocate(payloads[i].Bytes); }
            for (var attempt = 0; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                if (!isCurrent()) throw new ClipboardRequestCancelledException();
                if (api.Sequence != expectedSequence) throw new ClipboardWriteSupersededException();
                if (!api.Open(window))
                {
                    if (attempt >= 3) throw Busy();
                    await Task.Delay(attempt switch { 0 => 40, 1 => 100, _ => 200 }, ct);
                    continue;
                }
                try
                {
                    // Lock out competing writers, then recheck immediately before commit.
                    ct.ThrowIfCancellationRequested();
                    if (!isCurrent()) throw new ClipboardRequestCancelledException();
                    if (api.Sequence != expectedSequence) throw new ClipboardWriteSupersededException();
                    if (!api.Empty()) throw new Win32Exception();
                    try
                    {
                        // Publish the user content after the ownership metadata.
                        for (var i = 0; i < payloads.Length; i++)
                        {
                            if (!api.Publish(payloads[i].Format, handles[i])) throw new Win32Exception();
                            handles[i] = 0; // Windows now owns this HGLOBAL.
                        }
                    }
                    catch { api.Empty(); throw; }
                    return;
                }
                finally { api.Close(); }
            }
        }
        finally { foreach (var handle in handles) if (handle != 0) api.Free(handle); }
    }
    static COMException Busy() => new("Appunti temporaneamente non disponibili.", ClipboardFlushRetry.ClipboardBusy);
}

internal sealed class WindowsClipboardApi : INativeClipboardApi
{
    [DllImport("user32.dll", SetLastError = true)] static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] static extern nint SetClipboardData(uint format, nint memory);
    [DllImport("kernel32.dll", SetLastError = true)] static extern nint GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll")] static extern nint GlobalFree(nint memory);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint DragQueryFileW(nint drop, uint index, StringBuilder? path, uint capacity);

    public IReadOnlyList<string> ReadFiles()
    {
        const uint fileDropFormat = 15;
        var drop = NativeMethods.GetClipboardData(fileDropFormat);
        if (drop == 0) throw new COMException("Elenco file temporaneamente non disponibile.", ClipboardFlushRetry.ClipboardBusy);
        var count = DragQueryFileW(drop, uint.MaxValue, null, 0);
        if (count is 0 or > FileReferences.MaxItems)
            throw new InvalidDataException("Sono supportati fino a 1000 file o cartelle per copia.");
        var paths = new string[count];
        for (uint index = 0; index < count; index++)
        {
            var length = DragQueryFileW(drop, index, null, 0);
            if (length is 0 or > 32767) throw new InvalidDataException("Percorso file negli appunti non valido.");
            var path = new StringBuilder((int)length + 1);
            if (DragQueryFileW(drop, index, path, (uint)path.Capacity) != length)
                throw new InvalidDataException("Elenco file negli appunti non valido.");
            paths[index] = path.ToString();
        }
        return paths;
    }

    public uint Sequence => NativeMethods.GetClipboardSequenceNumber();
    public uint RegisterFormat(string name) => NativeMethods.RegisterClipboardFormat(name);
    public bool Contains(uint format) => NativeMethods.IsClipboardFormatAvailable(format);
    public bool Open(nint owner) => NativeMethods.OpenClipboard(owner);
    public void Close() => NativeMethods.CloseClipboard();
    public bool Empty() => EmptyClipboard();
    public bool Publish(uint format, nint handle) => SetClipboardData(format, handle) != 0;
    public void Free(nint handle) => GlobalFree(handle);
    public nint Allocate(byte[] bytes)
    {
        var handle = GlobalAlloc(0x0042, (nuint)bytes.Length); // GMEM_MOVEABLE | GMEM_ZEROINIT
        if (handle == 0) throw new OutOfMemoryException();
        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == 0) { Free(handle); throw new Win32Exception(); }
        try { Marshal.Copy(bytes, 0, pointer, bytes.Length); }
        catch { NativeMethods.GlobalUnlock(handle); Free(handle); throw; }
        NativeMethods.GlobalUnlock(handle); return handle;
    }
    public byte[]? Read(uint format, int maxBytes)
    {
        var handle = NativeMethods.GetClipboardData(format);
        if (handle == 0) return null;
        var size = NativeMethods.GlobalSize(handle);
        if (size == 0 || size > (nuint)maxBytes) return null;
        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == 0) return null;
        try { var bytes = new byte[(int)size]; Marshal.Copy(pointer, bytes, 0, bytes.Length); return bytes; }
        finally { NativeMethods.GlobalUnlock(handle); }
    }
}
