namespace ClipShelf.App.Desktop;
internal sealed class SingleInstanceService : IDisposable
{
    Mutex? mutex; bool acquired;
    public bool TryAcquire()
    {
        mutex = new Mutex(false, "Local\\ClipShelf.Personal.1");
        try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
        return acquired;
    }
    public void Dispose() { if (acquired) mutex?.ReleaseMutex(); mutex?.Dispose(); }
}
