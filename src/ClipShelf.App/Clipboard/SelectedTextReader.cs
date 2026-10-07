using ClipShelf.App.Interop;
using ClipShelf.Core.Selection;
using System.Windows.Automation;
namespace ClipShelf.App.Clipboard;
internal static class SelectedTextReader
{
    // Called only from an MTA worker. Providers can expose their document outside ControlView.
    public static SelectedText? Read(NativeMethods.Point end, NativeMethods.Point start, uint expectedProcess)
    {
        var candidates = new List<AutomationElement>();
        var blocked = false;
        void AddChain(AutomationElement? element)
        {
            for (var i = 0; element is not null && i < 32; i++)
            {
                var current = element.Current;
                if (current.ProcessId != expectedProcess) break;
                if (current.IsPassword || current.ProcessId == Environment.ProcessId) { blocked = true; return; }
                if (!candidates.Any(other => Automation.Compare(other, element))) candidates.Add(element);
                element = TreeWalker.RawViewWalker.GetParent(element);
            }
        }
        // The start remains inside the selected document even when the drag ends in a margin.
        foreach (var point in new[] { start, end })
        {
            try { AddChain(AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y))); }
            catch (ElementNotAvailableException) { }
            catch (System.Runtime.InteropServices.COMException) { }
        }
        if (blocked) return new(null, IsPassword: true);
        try
        {
            var focused = AutomationElement.FocusedElement;
            // Focus fallback must share a document/control with the gesture, not just its window.
            var chain = new List<AutomationElement>();
            for (var i = 0; focused is not null && i < 32; i++)
            {
                var current = focused.Current;
                if (current.ProcessId != expectedProcess || current.ControlType == ControlType.Window) break;
                if (current.IsPassword) return new(null, IsPassword:true);
                chain.Add(focused);
                focused = TreeWalker.RawViewWalker.GetParent(focused);
            }
            if (SelectionFocusScope.SharesTextOwner(candidates, chain, candidate => candidate.TryGetCurrentPattern(TextPattern.Pattern, out _), (a, b) => Automation.Compare(a, b)))
                foreach (var candidate in chain)
                    if (!candidates.Any(other => Automation.Compare(other, candidate))) candidates.Add(candidate);
        }
        catch (ElementNotAvailableException) { }
        catch (System.Runtime.InteropServices.COMException) { }
        foreach (var candidate in candidates)
        {
            try
            {
                if (!candidate.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) continue;
                var ranges = ((TextPattern)pattern).GetSelection();
                if (ranges is not { Length: 1 }) continue;
                var text = ranges[0].GetText(SelectionCopyCoordinator.MaxCharacters + 1);
                if (!string.IsNullOrWhiteSpace(text)) return new(text);
            }
            catch (ElementNotAvailableException) { }
            catch (InvalidOperationException) { }
            catch (System.Runtime.InteropServices.COMException) { }
        }
        return null;
    }
}
