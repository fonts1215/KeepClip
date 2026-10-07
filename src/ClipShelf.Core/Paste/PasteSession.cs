namespace ClipShelf.Core.Paste;
public sealed record PasteRequest(nint Destination, long Generation);
public sealed class PasteSession
{
    long generation; nint destination;
    public void Open(nint destination) { this.destination = destination; generation++; }
    public void Cancel() { destination = 0; generation++; }
    public PasteRequest Snapshot() => new(destination, generation);
    public bool IsCurrent(PasteRequest request) => request.Generation == generation;
}
