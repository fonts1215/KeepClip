using ClipShelf.App.Interop;
using ClipShelf.Core.Selection;
using System.Windows.Automation;
namespace ClipShelf.App.Clipboard;
internal static class SelectedTextReader
{
    // Called only from an MTA worker. UIA providers may perform cross-process work.
    public static SelectedText? Read(NativeMethods.Point point, uint expectedProcess)
    {
        var element = AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y));
        var ancestors = new List<AutomationElement>();
        for (var i = 0; element is not null && i < 16; i++)
        {
            var current = element.Current;
            if (current.IsPassword) return new(null, IsPassword: true);
            if (current.ProcessId == Environment.ProcessId) return new(null, IsOwnApp: true);
            if (current.ProcessId != expectedProcess) break;
            ancestors.Add(element);
            element = TreeWalker.ControlViewWalker.GetParent(element);
        }
        foreach (var candidate in ancestors)
        {
            if (!candidate.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) continue;
            var ranges = ((TextPattern)pattern).GetSelection();
            // Multiple disjoint selections are intentionally left to manual Copy.
            if (ranges.Length != 1) continue;
            var text = ranges[0].GetText(SelectionCopyCoordinator.MaxCharacters + 1);
            if (!string.IsNullOrWhiteSpace(text)) return new(text);
        }
        return null;
    }
}
