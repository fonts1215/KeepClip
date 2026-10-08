namespace KeepClip.Storage;
public sealed class HistoryStorageException(string path, Exception inner)
    : Exception($"Impossibile leggere la cronologia: {path}. Il file è stato preservato.", inner);
