using ClipShelf.App.Interop;
using ClipShelf.Core.Selection;
using System.Windows.Automation;
namespace ClipShelf.App.Clipboard;
internal static class SelectedTextReader
{
    // Called only from an MTA worker. Providers can expose their document outside ControlView.
    public static SelectedText? Read(NativeMethods.Point end, NativeMethods.Point start, uint expectedProcess, Action<string>? trace = null)
    {
        var candidates = new List<AutomationElement>();
        var blocked = false; var providers = 0; var empty = 0; var disjoint = 0; var mismatches = 0; var errors = 0;
        void AddChain(AutomationElement? element)
        {
            for (var i = 0; element is not null && i < 32; i++)
            {
                var current = element.Current;
                if (current.ProcessId != expectedProcess) { mismatches++; trace?.Invoke($"uia-process-mismatch expected={expectedProcess} actual={current.ProcessId} control={current.ControlType.ProgrammaticName}"); break; }
                if (current.IsPassword || current.ProcessId == Environment.ProcessId) { blocked = true; trace?.Invoke("uia-blocked reason=password-or-own"); return; }
                if (!candidates.Any(other => Automation.Compare(other, element))) candidates.Add(element);
                element = TreeWalker.RawViewWalker.GetParent(element);
            }
        }
        // The start remains inside the selected document even when the drag ends in a margin.
        foreach (var point in new[] { start, end })
        {
            try { AddChain(AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y))); }
            catch (ElementNotAvailableException ex) { errors++; trace?.Invoke($"uia-error type={ex.GetType().Name} hresult=0x{ex.HResult:X8}"); }
            catch (System.Runtime.InteropServices.COMException ex) { errors++; trace?.Invoke($"uia-error type={ex.GetType().Name} hresult=0x{ex.HResult:X8}"); }
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
                if (current.IsPassword) { trace?.Invoke("uia-blocked reason=password-focus"); return new(null, IsPassword:true); }
                chain.Add(focused);
                focused = TreeWalker.RawViewWalker.GetParent(focused);
            }
            if (SelectionFocusScope.SharesTextOwner(candidates, chain, candidate => candidate.TryGetCurrentPattern(TextPattern.Pattern, out _), (a, b) => Automation.Compare(a, b)))
                foreach (var candidate in chain)
                    if (!candidates.Any(other => Automation.Compare(other, candidate))) candidates.Add(candidate);
        }
        catch (ElementNotAvailableException ex) { errors++; trace?.Invoke($"uia-error type={ex.GetType().Name} hresult=0x{ex.HResult:X8}"); }
        catch (System.Runtime.InteropServices.COMException ex) { errors++; trace?.Invoke($"uia-error type={ex.GetType().Name} hresult=0x{ex.HResult:X8}"); }
        foreach (var candidate in candidates)
        {
            try
            {
                if (!candidate.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) continue;
                providers++;
                var ranges = ((TextPattern)pattern).GetSelection();
                if (ranges is not { Length: 1 }) { disjoint++; trace?.Invoke($"uia-ranges count={ranges?.Length ?? 0} "); continue; }
                var text = ranges[0].GetText(SelectionCopyCoordinator.MaxCharacters + 1);
                if (!string.IsNullOrWhiteSpace(text)) { trace?.Invoke($"uia-result found=true candidates={candidates.Count} providers={providers} "); return new(text); }
                empty++;
            }
            catch (ElementNotAvailableException ex) { errors++; trace?.Invoke($"uia-error type={ex.GetType().Name} hresult=0x{ex.HResult:X8}"); }
            catch (InvalidOperationException ex) { errors++; trace?.Invoke($"uia-error type={ex.GetType().Name} hresult=0x{ex.HResult:X8}"); }
            catch (System.Runtime.InteropServices.COMException ex) { errors++; trace?.Invoke($"uia-error type={ex.GetType().Name} hresult=0x{ex.HResult:X8}"); }
        }
        trace?.Invoke($"uia-result found=false candidates={candidates.Count} providers={providers} empty={empty} unsupportedRanges={disjoint} processMismatch={mismatches} errors={errors}");
        return null;
    }
}
