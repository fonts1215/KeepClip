using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using KeepClip.App.Interop;
using KeepClip.Core;
using KeepClip.Core.History;

namespace KeepClip.App.Clipboard;

internal interface INativeFileClipboardApi
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
}

internal sealed class NativeFileClipboard
{
    internal const string OwnerFormat = "KeepClip.Files.Owner.v1";
    const uint FileDropFormat = 15; // CF_HDROP
    readonly Func<nint> ownerWindow;
    readonly INativeFileClipboardApi api;
    readonly uint ownerFormat, effectFormat;
    public NativeFileClipboard(Func<nint> ownerWindow, INativeFileClipboardApi? api = null)
    {
        this.ownerWindow = ownerWindow; this.api = api ?? new WindowsFileClipboardApi();
        ownerFormat = this.api.RegisterFormat(OwnerFormat); effectFormat = this.api.RegisterFormat("Preferred DropEffect");
        if (ownerFormat == 0 || effectFormat == 0) throw new Win32Exception();
    }
    public string? ReadToken()
    {
        if (!api.Contains(ownerFormat)) return null;
        if (!api.Open(0)) throw Busy();
        try
        {
            var bytes = api.Read(ownerFormat, 2048);
            if (bytes is null || bytes.Length < 2 || bytes.Length % 2 != 0 || bytes[^1] != 0 || bytes[^2] != 0) return null;
            // GlobalSize can include zero padding beyond the requested allocation.
            var value = Encoding.Unicode.GetString(bytes);
            var terminator = value.IndexOf('\0');
            return terminator >= 0 ? value[..terminator] : null;
        }
        finally { api.Close(); }
    }
    public async Task WriteAsync(IReadOnlyList<ClipFile> files, string token, uint expectedSequence, Func<bool> isCurrent, CancellationToken ct)
    {
        if (!FileReferences.Valid(files)) throw new InvalidDataException("Selezione di file non valida.");
        if (string.IsNullOrEmpty(token) || token.Length > 1023 || token.Contains('\0')) throw new ArgumentException("Identificatore copia non valido.", nameof(token));
        var window = ownerWindow();
        if (window == 0) throw new InvalidOperationException("Finestra proprietaria degli appunti non disponibile.");
        // DROPFILES has a fixed 20-byte header followed by UTF-16 MULTI_SZ.
        var paths = Encoding.Unicode.GetBytes(string.Join('\0', files.Select(file => Path.GetFullPath(file.Path))) + "\0\0");
        var dropFiles = new byte[checked(20 + paths.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(dropFiles, 20);
        BinaryPrimitives.WriteUInt32LittleEndian(dropFiles.AsSpan(16), 1); // fWide
        paths.CopyTo(dropFiles, 20);
        (uint Format, byte[] Bytes)[] payloads = [(ownerFormat, Encoding.Unicode.GetBytes(token + "\0")), (effectFormat, [1, 0, 0, 0]), (FileDropFormat, dropFiles)];
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
                        // Publish the actual file list last; never expose a partial selection.
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
    static COMException Busy() => new("Appunti file temporaneamente non disponibili.", ClipboardFlushRetry.ClipboardBusy);
}

internal sealed class WindowsFileClipboardApi : INativeFileClipboardApi
{
    [DllImport("user32.dll", SetLastError = true)] static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] static extern nint SetClipboardData(uint format, nint memory);
    [DllImport("kernel32.dll", SetLastError = true)] static extern nint GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll")] static extern nint GlobalFree(nint memory);
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
