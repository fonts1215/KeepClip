using System.Runtime.InteropServices;
using ClipShelf.App.Interop;
using ClipShelf.Core.History;
namespace ClipShelf.App.Clipboard;
internal static class NativeClipboardImage
{
    static readonly uint pngFormat = NativeMethods.RegisterClipboardFormat("PNG");
    public static byte[]? ReadPng()
    {
        if (pngFormat == 0 || !NativeMethods.IsClipboardFormatAvailable(pngFormat)) return null;
        if (!NativeMethods.OpenClipboard(0)) throw Busy();
        try
        {
            var memory = NativeMethods.GetClipboardData(pngFormat);
            if (memory == 0) throw Busy();
            var size = NativeMethods.GlobalSize(memory);
            if (size == 0) throw new InvalidDataException("Immagine negli appunti non disponibile.");
            if (size > (nuint)HistoryPolicy.MaxImageBytes) throw new InvalidDataException("Immagine troppo grande: massimo 20 MiB.");
            var pointer = NativeMethods.GlobalLock(memory); if (pointer == 0) throw Busy();
            try { var bytes = new byte[checked((int)size)]; Marshal.Copy(pointer, bytes, 0, bytes.Length); return bytes; }
            finally { NativeMethods.GlobalUnlock(memory); }
        }
        finally { NativeMethods.CloseClipboard(); }
    }
    static COMException Busy() => new("Appunti immagine temporaneamente non disponibili.", unchecked((int)0x800401D0));
}
