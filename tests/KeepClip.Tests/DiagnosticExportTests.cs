using KeepClip.App;

namespace KeepClip.Tests;

public class DiagnosticExportTests : IDisposable
{
    readonly string folder = Directory.CreateTempSubdirectory("keepclip-export-").FullName;

    [Fact]
    public void ExportIncludesBothLogGenerationsWithoutStartupStackOrHistory()
    {
        File.WriteAllText(Path.Combine(folder, "diagnostic.log.previous"), "2026-11-06T09:00:00Z app-ready paused=False");
        File.WriteAllText(Path.Combine(folder, "diagnostic.log"),
            "2026-11-06T09:01:00Z app-start-failed stage=history exception=IOException hresult=0x80131620 stack= at Start() in C:\\Users\\Private\\App.cs:line 1\n" +
            "2026-11-06T09:02:00Z clipboard-write kind=Text sequence=17");
        File.WriteAllText(Path.Combine(folder, "history.db"), "clipboard secret");

        var report = Assert.IsType<string>(DiagnosticLog.ReadForExport(folder));
        Assert.True(report.IndexOf("app-ready", StringComparison.Ordinal) < report.IndexOf("app-start-failed", StringComparison.Ordinal));
        Assert.Contains("stage=history exception=IOException hresult=0x80131620", report);
        Assert.Contains("clipboard-write kind=Text sequence=17", report);
        Assert.DoesNotContain("stack=", report);
        Assert.DoesNotContain("Private", report);
        Assert.DoesNotContain("clipboard secret", report);
    }

    [Fact]
    public void EmptyLogsProduceNoExport()
    {
        File.WriteAllText(Path.Combine(folder, "diagnostic.log"), "");
        Assert.Null(DiagnosticLog.ReadForExport(folder));
    }

    public void Dispose()
    {
        foreach (var path in Directory.EnumerateFiles(folder)) File.Delete(path);
        Directory.Delete(folder);
    }
}

