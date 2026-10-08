namespace KeepClip.App;
internal static class DiagnosticLog
{
    static readonly object gate = new();
    public static string? DirectoryPath { get; set; }
    public static void Write(string eventName)
    {
        var folder = DirectoryPath; if (folder is null) return;
        var timestamp = DateTimeOffset.UtcNow;
        _ = Task.Run(() =>
        {
            try
            {
                lock (gate)
                {
                    Directory.CreateDirectory(folder); var path = Path.Combine(folder, "diagnostic.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Move(path, path + ".previous", true);
                    File.AppendAllText(path, $"{timestamp:O} {eventName}{Environment.NewLine}");
                }
            }
            catch { /* Diagnostics must not break clipboard capture. */ }
        });
    }
}
