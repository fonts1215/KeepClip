namespace ClipShelf.Core.History;

public enum ClipKind { Text, Image }
public sealed record ClipContent(ClipKind Kind, string? Text = null, byte[]? Png = null);
public sealed record ClipEntry(Guid Id, ClipKind Kind, string Hash, DateTimeOffset CopiedAt,
    bool IsPinned, string? Text, string? ImagePath, long SizeBytes);
public sealed record HistoryDecision(IReadOnlyList<ClipEntry> Entries, IReadOnlyList<Guid> RemovedIds, string? Rejection);
