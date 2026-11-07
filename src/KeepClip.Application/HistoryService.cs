using KeepClip.Application.History;
using KeepClip.Domain.History;
namespace KeepClip.Application;
public sealed record CaptureResult(bool Captured, string? Message = null);
public sealed class HistoryService(IHistoryRepository repository, Func<Guid, byte[], CancellationToken, Task<string>> saveImage, Func<ClipboardSourceContext, Task<ClipSource?>>? resolveSource = null)
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
            if (snapshot.PrepareContent is { } prepare)
            {
                content = await Task.Run(prepare, ct);
                ct.ThrowIfCancellationRequested();
                if (paused || (snapshot.CaptureGeneration.HasValue && snapshot.CaptureGeneration != CaptureGeneration)) return new(false);
            }
            var (candidate, rejection) = HistoryCandidate.Prepare(content);
            if (candidate is null) return new(false, rejection);
            var source = snapshot.Source;
            if (source is null && snapshot.SourceContext is { } context && resolveSource is not null)
            {
                source = await resolveSource(context);
                ct.ThrowIfCancellationRequested();
                if (paused || (snapshot.CaptureGeneration.HasValue && snapshot.CaptureGeneration != CaptureGeneration)) return new(false);
            }
            var id = Guid.NewGuid();
            var incoming = candidate.CreateEntry(id, DateTimeOffset.UtcNow, source);
            var decision = policy.Apply(Entries, incoming);
            if (decision.Rejection is { } reason)
                return new(false, reason switch
                {
                    HistoryRejection.ContentTooLarge => "Contenuto troppo grande per la cronologia.",
                    HistoryRejection.CapacityReservedForFavorites => "Limite raggiunto: rimuovi un preferito per acquisire nuovi elementi.",
                    _ => throw new ArgumentOutOfRangeException(nameof(reason))
                });
            var list = decision.Entries.ToArray();
            if (candidate.ImageBytes is { } imageBytes && list.Any(x => x.Id == id))
            {
                var path = await saveImage(id, imageBytes, ct); list = list.Select(x => x.Id == id ? x with { ImagePath = path } : x).ToArray();
            }
            await repository.SaveAsync(list, ct); Entries = list; HistoryChanged?.Invoke(); return new(true);
        }
        finally { gate.Release(); }
    }
    public Task TogglePinAsync(Guid id) => MutateAsync(entries => HistoryPolicy.TogglePin(entries, id));
    public Task DeleteAsync(Guid id) => MutateAsync(entries => HistoryPolicy.Delete(entries, id));
    public Task ClearAsync(bool includePinned) => MutateAsync(entries => HistoryPolicy.Clear(entries, includePinned));
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
