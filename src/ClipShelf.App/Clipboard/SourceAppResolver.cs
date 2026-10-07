using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ClipShelf.Core.History;
using ClipShelf.App.Interop;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage.Streams;
namespace ClipShelf.App.Clipboard;
internal static class SourceAppResolver
{
    [DllImport("user32.dll")] static extern nint GetClipboardOwner();
    [DllImport("kernel32.dll")] static extern nint OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder? value);
    public static uint ClipboardProcess()
    {
        NativeMethods.GetWindowThreadProcessId(GetClipboardOwner(), out var process);
        return process == (uint)Environment.ProcessId ? 0 : process;
    }
    public static async Task<ClipSource?> ResolveAsync(uint process)
    {
        if (process == 0 || process == (uint)Environment.ProcessId) return null;
        try
        {
            var identity = await Task.Run(() =>
            {
                using var app = Process.GetProcessById(checked((int)process));
                string? path = null; string name = app.ProcessName;
                try { path = app.MainModule?.FileName; if (path is not null) name = FileVersionInfo.GetVersionInfo(path).FileDescription ?? name; } catch { }
                string? appId = null; var handle = OpenProcess(0x1000, false, process);
                if (handle != 0)
                {
                    try
                    {
                        uint length = 0;
                        if (GetApplicationUserModelId(handle, ref length, null) == 122 && length is > 0 and < 4096)
                        {
                            var value = new StringBuilder((int)length);
                            if (GetApplicationUserModelId(handle, ref length, value) == 0) appId = value.ToString();
                        }
                    }
                    finally { CloseHandle(handle); }
                }
                return (path, name, appId);
            });
            if (identity.appId is not null && ApiInformation.IsMethodPresent("Windows.ApplicationModel.AppInfo", "GetFromAppUserModelId"))
            {
                try
                {
                    var info = AppInfo.GetFromAppUserModelId(identity.appId);
                    using var stream = await info.DisplayInfo.GetLogo(new Size(64,64)).OpenReadAsync();
                    if (stream.Size <= 262144)
                    {
                        using var reader = new DataReader(stream);
                        await reader.LoadAsync((uint)stream.Size);
                        var bytes = new byte[(int)stream.Size]; reader.ReadBytes(bytes);
                        return new(info.DisplayInfo.DisplayName, bytes);
                    }
                    return new(info.DisplayInfo.DisplayName);
                }
                catch { }
            }
            byte[]? icon = await Task.Run(() =>
            {
                try
                {
                    if (identity.path is null) return null;
                    using var original = System.Drawing.Icon.ExtractAssociatedIcon(identity.path);
                    if (original is null) return null;
                    using var bitmap = original.ToBitmap();
                    using var small = new System.Drawing.Bitmap(bitmap, 64, 64);
                    using var output = new MemoryStream();
                    small.Save(output, System.Drawing.Imaging.ImageFormat.Png);
                    return output.ToArray();
                }
                catch { return null; }
            });
            return new(string.IsNullOrWhiteSpace(identity.name) ? "Origine sconosciuta" : identity.name.Trim(), icon);
        }
        catch { return null; }
    }
}
