using KeepClip.App;
namespace KeepClip.Tests;

public class StartupDiagnosticsTests
{
    [Fact]
    public void StartupFailureIsPersistedBeforeReturningWithoutExceptionPayload()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KeepClip.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            DiagnosticLog.DirectoryPath = directory;
            DiagnosticLog.StartupFailure("shelf-theme", new InvalidOperationException("sensitive-content"));
            var log = File.ReadAllText(Path.Combine(directory, "diagnostic.log"));
            Assert.Contains("stage=shelf-theme", log);
            Assert.Contains("exception=InvalidOperationException", log);
            Assert.DoesNotContain("sensitive-content", log);
        }
        finally { DiagnosticLog.DirectoryPath = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
