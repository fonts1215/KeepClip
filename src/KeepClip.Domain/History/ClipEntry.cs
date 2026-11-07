namespace KeepClip.Domain.History;

public enum ClipKind { Text, Image, Files }
public sealed record ClipSource(string Name, byte[]? IconPng = null);
public sealed record ClipFile(string Path, bool IsDirectory = false);
public sealed record ClipContent(ClipKind Kind, string? Text = null, byte[]? Png = null, IReadOnlyList<ClipFile>? Files = null);
public sealed record ClipEntry(Guid Id, ClipKind Kind, string Hash, DateTimeOffset CopiedAt,
    bool IsPinned, string? Text, string? ImagePath, long SizeBytes, ClipSource? Source = null, IReadOnlyList<ClipFile>? Files = null);
public enum HistoryRejection { ContentTooLarge, CapacityReservedForFavorites }
public sealed record HistoryDecision(IReadOnlyList<ClipEntry> Entries, IReadOnlyList<Guid> RemovedIds, HistoryRejection? Rejection);
