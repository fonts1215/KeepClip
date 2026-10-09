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
    public static void StartupFailure(string stage, Exception error)
    {
        // Persist before Exit: a queued Task.Run can be lost when startup fails.
        var folder = DirectoryPath; if (folder is null) return;
        try
        {
            var cause = error.GetBaseException();
            var trace = error.StackTrace?.Replace(Environment.NewLine, " | ") ?? "unavailable";
            lock (gate)
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "diagnostic.log"),
                    $"{DateTimeOffset.UtcNow:O} app-start-failed stage={stage} exception={error.GetType().Name} hresult=0x{error.HResult:X8} cause={cause.GetType().Name} causeHresult=0x{cause.HResult:X8} stack={trace}{Environment.NewLine}");
            }
        }
        catch { /* Failure diagnostics must not prevent shutdown. */ }
    }
}
