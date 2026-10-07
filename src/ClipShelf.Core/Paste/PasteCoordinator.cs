using ClipShelf.Core.History;
namespace ClipShelf.Core.Paste;
public interface IWindowTarget
{
    nint Capture(); bool IsValid(nint handle); Task<bool> TryActivateAsync(nint handle, CancellationToken ct); bool IsForeground(nint handle);
}
public interface IPasteInput
{
    Task<bool> WaitForModifiersReleasedAsync(TimeSpan timeout, CancellationToken ct); bool SendPaste();
}
public enum PasteOutcome { InputSent, CopiedOnly }
public sealed record PasteResult(PasteOutcome Outcome, string? Message = null);
public sealed class PasteCoordinator(IClipboardAdapter clipboard, IWindowTarget target, IPasteInput input, Action hide)
{
    public async Task<PasteResult> PasteAsync(ClipContent content, nint destination, CancellationToken ct = default)
    {
        await clipboard.WriteAsync(content, Guid.NewGuid(), ct);
        hide();
        PasteResult Fallback() => new(PasteOutcome.CopiedOnly, "Elemento copiato. Torna all'app desiderata e usa Ctrl+V.");
        if (!target.IsValid(destination)) return Fallback();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            if (!await target.TryActivateAsync(destination, timeout.Token)) return Fallback();
            if (!await input.WaitForModifiersReleasedAsync(TimeSpan.FromSeconds(1), timeout.Token)) return Fallback();
            if (!target.IsValid(destination) || !target.IsForeground(destination)) return Fallback();
            return input.SendPaste() ? new(PasteOutcome.InputSent) : Fallback();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Fallback(); }
    }
}
