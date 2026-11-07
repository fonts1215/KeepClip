using KeepClip.Domain.History;

namespace KeepClip.Application;

public sealed record ClipSourceCandidate(string ProcessName, ClipSource? Source);

public static class SourceAttribution
{
    public static bool NeedsForeground(ClipSourceCandidate? owner) => owner?.Source is null || IsIntermediary(owner.ProcessName);
    public static ClipSource? Select(ClipSourceCandidate? owner, ClipSourceCandidate? foreground) =>
        !NeedsForeground(owner) ? owner!.Source :
        foreground is not null && !IsIntermediary(foreground.ProcessName) ? foreground.Source : null;

    public static bool IsIntermediary(string processName) =>
        processName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("RuntimeBroker", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("svchost", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("taskhostw", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("rundll32", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("dllhost", StringComparison.OrdinalIgnoreCase);
}

public sealed record ClipboardSourceContext(uint Sequence, uint OwnerProcess, uint ForegroundProcess);

// Notification thread and clipboard STA worker share only this immutable snapshot.
public sealed class ClipboardSourceTracker
{
    readonly object gate = new();
    ClipboardSourceContext? latest;

    public void Record(ClipboardSourceContext context) { lock (gate) latest = context; }
    public ClipboardSourceContext? ForSequence(uint sequence)
    {
        lock (gate) return latest?.Sequence == sequence ? latest : null;
    }
}
