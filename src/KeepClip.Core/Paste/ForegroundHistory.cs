namespace KeepClip.Core.Paste;
public sealed class ForegroundHistory(Func<nint, bool> isDestination)
{
    nint previous;
    public void Observe(nint window) { if (isDestination(window)) previous = window; }
    public nint Resolve(nint currentWindow)
    {
        Observe(currentWindow);
        return isDestination(previous) ? previous : 0;
    }
}
