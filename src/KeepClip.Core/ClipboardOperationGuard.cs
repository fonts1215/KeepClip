using System.Runtime.InteropServices;
namespace KeepClip.Core;
// A sequence can advance when a delayed format is rendered; check the operation
// marker before treating that change as another copy.
public sealed class ClipboardOperationGuard(string operationToken,
    Func<uint> sequence, Func<CancellationToken, Task<string?>> readOperationToken)
{
    uint? confirmedSequence;
    public async Task<bool> StillOwnedAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var observed = sequence();
        if (observed == confirmedSequence) return true;
        var token = await readOperationToken(ct);
        ct.ThrowIfCancellationRequested();
        if (sequence() != observed)
            throw new COMException("Gli appunti sono cambiati durante la verifica.", ClipboardFlushRetry.ClipboardBusy);
        if (token != operationToken) return false;
        confirmedSequence = observed; return true;
    }
}
