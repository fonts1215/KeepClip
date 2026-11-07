namespace KeepClip.Application.Paste;
public sealed record PasteRequest(nint Destination, long Generation);
public sealed class PasteSession
{
    long generation; nint destination;
    public void Open(nint destination) { this.destination = destination; Interlocked.Increment(ref generation); }
    public void Cancel() { destination = 0; Interlocked.Increment(ref generation); }
    public PasteRequest Snapshot() => new(destination, generation);
    public bool IsCurrent(PasteRequest request) => request.Generation == Volatile.Read(ref generation);
}
