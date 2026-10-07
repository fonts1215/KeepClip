namespace ClipShelf.Core.Selection;
public static class SelectionReadSafety
{
    // Unsupported/mixed metadata and provider failures cannot authorize a clipboard write.
    public static bool? ProbeReadOnly(Func<object?> readAttribute, Action<Exception>? failed = null)
    {
        try { return readAttribute() is bool readOnly ? readOnly : null; }
        catch (Exception ex) { failed?.Invoke(ex); return null; }
    }
}
