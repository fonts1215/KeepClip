using ClipShelf.App.Clipboard;
using ClipShelf.App.Desktop;
using ClipShelf.App.Interop;
using ClipShelf.App.Settings;
using ClipShelf.App.Shelf;
using ClipShelf.Core;
using ClipShelf.Core.Selection;
using ClipShelf.Storage;
using Microsoft.UI.Xaml;
namespace ClipShelf.App;
public partial class App : Application
{
    readonly SingleInstanceService instance = new();
    MessageWindow? messages; ClipboardMonitor? monitor; HotkeyService? hotkey; TrayIconService? tray;
    ShelfWindow? shelf; SettingsWindow? settingsWindow; HistoryService? history;
    AppSettings settings = AppSettings.Default;
    SettingsStore? settingsStore;
    ForegroundTracker? foreground;
    SelectionCopyService? selectionCopy;
    string dataPath = "";
    bool storageReady, shuttingDown;
    public App() { InitializeComponent(); UnhandledException += (_, e) => { e.Handled = true; Notify("Si è verificato un errore. Se persiste, riavvia KeepClip."); }; }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!instance.TryAcquire()) { Exit(); return; }
        try
        {
            try { dataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path; }
            catch { dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipShelf"); }
            DiagnosticLog.DirectoryPath = dataPath; DiagnosticLog.Write("app-start diagnostics=selection-v2");
            settingsStore = new(dataPath); string? startupError = null;
            try { settings = await settingsStore.LoadAsync(); } catch { startupError = "Impostazioni non leggibili. Sono stati usati i valori predefiniti."; }
            var repository = new SqliteHistoryRepository(dataPath); var images = new ImageStore(dataPath);
            history = new(repository, images.SaveAsync); history.SetPaused(settings.IsPaused);
            foreground = new();
            var clipboard = new ClipboardAdapter(); shelf = new(history, clipboard);
            messages = new(); hotkey = new(messages); tray = new(messages) { Paused = settings.IsPaused };
            tray.Open += OpenShelf; tray.Settings += OpenSettings; tray.TogglePause += TogglePause; tray.Exit += Shutdown;
            tray.Show(); shelf.SettingsRequested += OpenSettings; shelf.Notification += Notify; hotkey.Invoked += OpenShelf;
            if (!hotkey.TryRegister(settings.Hotkey)) startupError = "Scorciatoia occupata. Apri le impostazioni dall'icona e scegli un'altra combinazione.";
            try { await history.InitializeAsync(); storageReady = true; }
            catch (Exception ex) { startupError = ex is HistoryStorageException ? ex.Message : "Impossibile aprire la cronologia. Controlla la cartella dati nelle impostazioni."; history.SetPaused(true); }
            monitor = new(messages, clipboard, history); monitor.Problem += Notify; monitor.Copied += OnCopied;
            var selectionCoordinator = new SelectionCopyCoordinator(clipboard, history, () => settings.CopyOnSelection, NativeMethods.GetClipboardSequenceNumber, OnCopied, Notify);
            selectionCopy = new(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(), history, selectionCoordinator, () => settings.CopyOnSelection && storageReady && !history.IsPaused);
            selectionCopy.Problem += Notify;
            if (!UpdateSelectionCapture()) startupError = "Impossibile attivare la copia alla selezione. Le copie manuali restano disponibili.";
            DiagnosticLog.Write($"app-ready paused={history.IsPaused}");
            var startupActivation = false;
            try { startupActivation = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs().Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.StartupTask; } catch { }
            if (!StartupCommand.ShouldStartHidden(Environment.GetCommandLineArgs(), startupActivation)) shelf.ShowFor(0);
            if (startupError is not null) Notify(startupError);
            else if (settings.IsPaused) shelf.ShowMessage("La cattura è in pausa. Riprendila dall'icona nell'area di notifica.");
        }
        catch (Exception ex)
        {
            // Diagnostic log contains exception type only, never clipboard payloads.
            System.Diagnostics.Debug.WriteLine(ex.GetType().Name);
            if (shelf is not null) { shelf.ShowFor(0); shelf.ShowMessage("Avvio incompleto. Verifica le dipendenze e riavvia KeepClip."); }
            else Shutdown();
        }
    }
    void OpenShelf()
    {
        if (shelf is null) return;
        if (shelf.IsVisible) { shelf.Hide(); return; }
        shelf.ShowFor(foreground?.Capture() ?? new WindowTarget().Capture());
    }
    void OpenSettings()
    {
        shelf?.Hide();
        if (settingsWindow is null)
        {
            settingsWindow = new(settings, dataPath, ApplySettingsAsync, RecoverAsync);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
        }
        settingsWindow.Activate();
    }
    async void TogglePause()
    {
        try { var result = await ApplySettingsAsync(settings with { IsPaused = !settings.IsPaused }); Notify(result ?? (settings.IsPaused ? "Cattura in pausa." : "Cattura ripresa.")); }
        catch { Notify("Impossibile salvare la pausa."); }
    }
    async Task<string?> ApplySettingsAsync(AppSettings next)
    {
        if (hotkey is null || history is null || settingsStore is null) return "Applicazione non inizializzata.";
        var previous = settings;
        if (!hotkey.TryRegister(next.Hotkey)) return "Scorciatoia già in uso. La combinazione precedente resta attiva.";
        var requestedAutoStart = next.AutoStart;
        var startup = new StartupService(); var current = await startup.GetAsync();
        var actual = current.Available && current.Enabled != next.AutoStart ? await startup.SetEnabledAsync(next.AutoStart) : current;
        next = next with { AutoStart = actual.Enabled };
        try { await settingsStore.SaveAsync(next); }
        catch { hotkey.TryRegister(previous.Hotkey); if (actual.Enabled != current.Enabled) await startup.SetEnabledAsync(current.Enabled); throw; }
        settings = next; history.SetPaused(next.IsPaused || !storageReady); if (tray is not null) tray.Paused = next.IsPaused || !storageReady;
        if (!UpdateSelectionCapture()) return "Impostazioni salvate, ma la copia alla selezione non è disponibile. Riprova dopo il riavvio.";
        return !storageReady ? "Cronologia non disponibile. Ripristinala prima di riprendere la cattura." :
            (!actual.Available || actual.Enabled != requestedAutoStart) ? actual.Message ?? "Windows non ha applicato la scelta di avvio automatico." : null;
    }
    async Task RecoverAsync()
    {
        if (history is null) return;
        history.SetPaused(true); storageReady = false;
        UpdateSelectionCapture();
        // Wait for any in-flight capture to finish before archiving the database and images.
        await history.ResetWithBackupAsync(new HistoryRecovery(dataPath).PrepareResetAsync);
        storageReady = true; history.SetPaused(settings.IsPaused); if (tray is not null) tray.Paused = settings.IsPaused;
        UpdateSelectionCapture();
    }
    void Notify(string message) { shelf?.ShowMessage(message); tray?.Notify(message); }
    void OnCopied() { if (settings.CopySound && history?.IsPaused == false) CopyFeedbackSound.Play(); }
    bool UpdateSelectionCapture()
    {
        if (selectionCopy is null) return true;
        if (settings.CopyOnSelection && storageReady && history?.IsPaused == false) return selectionCopy.Start();
        selectionCopy.Stop(); return true;
    }
    void Shutdown()
    {
        if (shuttingDown) return; shuttingDown = true;
        selectionCopy?.Dispose(); foreground?.Dispose(); monitor?.Dispose(); hotkey?.Dispose(); tray?.Dispose(); messages?.Dispose(); settingsWindow?.Close(); shelf?.ExitWindow(); instance.Dispose(); Exit();
    }
}
