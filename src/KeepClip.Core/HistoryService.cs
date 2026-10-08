using KeepClip.Core.History;
using System.Security.Cryptography;
using System.Text;
namespace KeepClip.Core;
public sealed record CaptureResult(bool Captured, string? Message = null);
public sealed class HistoryService(IHistoryRepository repository, Func<Guid, byte[], CancellationToken, Task<string>> saveImage)
{
    public IReadOnlyList<ClipEntry> Entries { get; private set; } = [];
    public event Action? HistoryChanged;
    readonly SemaphoreSlim gate = new(1, 1);
    readonly HistoryPolicy policy = new();
    volatile bool paused;
    long captureGeneration;
    public bool IsPaused => paused;
    public long CaptureGeneration => Interlocked.Read(ref captureGeneration);
    uint? lastSequence;
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try { Entries = await repository.LoadAsync(ct); HistoryChanged?.Invoke(); }
        finally { gate.Release(); }
    }
    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value; Interlocked.Increment(ref captureGeneration);
    }
    public async Task<CaptureResult> CaptureAsync(ClipboardSnapshot snapshot, CancellationToken ct = default)
    {
        if (paused || snapshot.IsOwnWrite || (snapshot.CaptureGeneration.HasValue && snapshot.CaptureGeneration != CaptureGeneration)) return new(false);
        await gate.WaitAsync(ct);
        try
        {
            if (paused || (snapshot.CaptureGeneration.HasValue && snapshot.CaptureGeneration != CaptureGeneration) ||
                (lastSequence.HasValue && unchecked((int)(snapshot.Sequence - lastSequence.Value)) <= 0)) return new(false);
            lastSequence = snapshot.Sequence;
            var content = snapshot.Content;
            byte[] bytes;
            if (content.Kind == ClipKind.Text && !string.IsNullOrEmpty(content.Text)) bytes = Encoding.UTF8.GetBytes(content.Text);
            else if (content.Kind == ClipKind.Image && content.Png is { Length: > 0 }) bytes = content.Png;
            else if (content.Kind == ClipKind.Files)
            {
                if (!FileReferences.Valid(content.Files)) return new(false, "Selezione di file non valida: sono supportati fino a 1000 file o cartelle con percorso disponibile.");
                bytes = FileReferences.Identity(content.Files!);
            }
            else return new(false);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)); var id = Guid.NewGuid();
            var incoming = new ClipEntry(id, content.Kind, hash, DateTimeOffset.UtcNow, false, content.Kind == ClipKind.Text ? content.Text : null, null, bytes.LongLength, snapshot.Source, content.Kind == ClipKind.Files ? content.Files!.ToArray() : null);
            var decision = policy.Apply(Entries, incoming);
            if (decision.Rejection is not null) return new(false, decision.Rejection);
            var list = decision.Entries.ToArray();
            if (content.Kind == ClipKind.Image && list.Any(x => x.Id == id))
            {
                var path = await saveImage(id, bytes, ct); list = list.Select(x => x.Id == id ? x with { ImagePath = path } : x).ToArray();
            }
            await repository.SaveAsync(list, ct); Entries = list; HistoryChanged?.Invoke(); return new(true);
        }
        finally { gate.Release(); }
    }
    public Task TogglePinAsync(Guid id) => MutateAsync(entries => entries.Select(x => x.Id == id ? x with { IsPinned = !x.IsPinned } : x).ToArray());
    public Task DeleteAsync(Guid id) => MutateAsync(entries => entries.Where(x => x.Id != id).ToArray());
    public Task ClearAsync(bool includePinned) => MutateAsync(entries => entries.Where(x => !includePinned && x.IsPinned).ToArray());
    async Task MutateAsync(Func<IReadOnlyList<ClipEntry>, IReadOnlyList<ClipEntry>> mutate)
    {
        await gate.WaitAsync();
        try { var next = mutate(Entries); await repository.SaveAsync(next); Entries = next; HistoryChanged?.Invoke(); }
        finally { gate.Release(); }
    }
    public async Task ResetWithBackupAsync(Func<Task> backup)
    {
        await gate.WaitAsync();
        try { await backup(); await repository.SaveAsync([]); Entries = []; HistoryChanged?.Invoke(); }
        finally { gate.Release(); }
    }
}
