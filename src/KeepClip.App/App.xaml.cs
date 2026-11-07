using KeepClip.App.Clipboard;
using KeepClip.App.Desktop;
using KeepClip.App.Interop;
using KeepClip.App.Settings;
using KeepClip.App.Shelf;
using KeepClip.Application;
using KeepClip.Application.Selection;
using KeepClip.Storage;
using Microsoft.UI.Xaml;
namespace KeepClip.App;
public partial class App : Microsoft.UI.Xaml.Application
{
    readonly SingleInstanceService instance = new();
    MessageWindow? messages; ClipboardMonitor? monitor; HotkeyService? hotkey; TrayIconService? tray;
    ShelfWindow? shelf; SettingsWindow? settingsWindow; HistoryService? history;
    AppSettings settings = AppSettings.Default;
    SettingsStore? settingsStore;
    ForegroundTracker? foreground;
    SelectionCopyService? selectionCopy;
    AsyncClipboardService? clipboard;
    string dataPath = "";
    bool storageReady, shuttingDown;
    public App() { InitializeComponent(); UnhandledException += (_, e) => { e.Handled = true; Notify("Si è verificato un errore. Se persiste, riavvia KeepClip."); }; }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!instance.TryAcquire()) { Exit(); return; }
        var startupStage = "data-path";
        try
        {
            try { dataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path; }
            catch { dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeepClip"); }
            DiagnosticLog.DirectoryPath = dataPath; DiagnosticLog.Write("app-start diagnostics=native-clipboard-v2");
            startupStage = "settings";
            settingsStore = new(dataPath); string? startupError = null;
            try { settings = await settingsStore.LoadAsync(); } catch { startupError = "Impostazioni non leggibili. Sono stati usati i valori predefiniti."; }
            startupStage = "history-services";
            var repository = new SqliteHistoryRepository(dataPath); var images = new ImageStore(dataPath);
            history = new(repository, images.SaveAsync, context => SourceAppResolver.ResolveClipboardAsync(context.OwnerProcess, context.ForegroundProcess));
            history.SetPaused(settings.IsPaused);
            startupStage = "foreground-tracker"; foreground = new();
            startupStage = "clipboard-service";
            var sources = new ClipboardSourceTracker();
            clipboard = new(new ClipboardAdapter(sources, () => messages?.Handle ?? 0), new StaClipboardExecutor(), NativeMethods.GetClipboardSequenceNumber);
            startupStage = "shelf-window"; shelf = new(history, clipboard, new StoredClipContentLoader());
            startupStage = "shelf-theme";
            shelf.ApplyAppearance(settings.Appearance);
            startupStage = "desktop-services";
            messages = new(); hotkey = new(messages); tray = new(messages) { Paused = settings.IsPaused };
            tray.Open += OpenShelf; tray.Settings += OpenSettings; tray.TogglePause += TogglePause; tray.Exit += Shutdown;
            tray.Show(); shelf.SettingsRequested += OpenSettings; shelf.Notification += Notify; hotkey.Invoked += OpenShelf;
            if (!hotkey.TryRegister(settings.Hotkey)) startupError = "Scorciatoia occupata. Apri le impostazioni dall'icona e scegli un'altra combinazione.";
            startupStage = "history-load";
            try { await history.InitializeAsync(); storageReady = true; }
            catch (Exception ex) { startupError = ex is HistoryStorageException ? ex.Message : "Impossibile aprire la cronologia. Controlla la cartella dati nelle impostazioni."; history.SetPaused(true); }
            startupStage = "capture-services";
            monitor = new(messages, clipboard, history, sources); monitor.Problem += Notify; monitor.Copied += OnCopied;
            var selectionCoordinator = new SelectionCopyCoordinator(clipboard, history, () => settings.CopyOnSelection, NativeMethods.GetClipboardSequenceNumber, OnCopied, Notify);
            selectionCopy = new(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(), history, selectionCoordinator, () => settings.CopyOnSelection && storageReady && !history.IsPaused);
            selectionCopy.Problem += Notify;
            if (!UpdateSelectionCapture()) startupError = "Impossibile attivare la copia alla selezione. Le copie manuali restano disponibili.";
            if (!hotkey.SetWindowsV(settings.UseWindowsV))
                startupError ??= "Win+V non disponibile. Usa la scorciatoia alternativa di KeepClip.";
            DiagnosticLog.Write($"app-ready paused={history.IsPaused}");
            var startupActivation = false;
            try { startupActivation = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs().Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.StartupTask; } catch { }
            startupStage = "show-window";
            if (!StartupCommand.ShouldStartHidden(Environment.GetCommandLineArgs(), startupActivation)) shelf.ShowFor(0);
            if (startupError is not null) Notify(startupError);
            else if (settings.IsPaused) shelf.ShowMessage("La cattura è in pausa. Riprendila dall'icona nell'area di notifica.");
        }
        catch (Exception ex)
        {
            // Diagnostic log contains exception type only, never clipboard payloads.
            DiagnosticLog.StartupFailure(startupStage, ex);
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
        var alternativeAvailable = hotkey.TryRegister(next.Hotkey);
        if (!alternativeAvailable)
        {
            if (!previous.UseWindowsV || next.UseWindowsV)
                return "Scorciatoia già in uso. La combinazione precedente resta attiva.";
            // Releasing Win+V must also work when another app has the alternate shortcut.
            next = next with { Hotkey = previous.Hotkey };
        }
        var requestedAutoStart = next.AutoStart;
        var startup = new StartupService(); var current = await startup.GetAsync();
        var actual = current.Available && current.Enabled != next.AutoStart ? await startup.SetEnabledAsync(next.AutoStart) : current;
        next = next with { AutoStart = actual.Enabled };
        try { await settingsStore.SaveAsync(next); }
        catch { hotkey.TryRegister(previous.Hotkey); if (actual.Enabled != current.Enabled) await startup.SetEnabledAsync(current.Enabled); throw; }
        settings = next; history.SetPaused(next.IsPaused || !storageReady); if (tray is not null) tray.Paused = next.IsPaused || !storageReady;
        var windowsVAvailable = hotkey.SetWindowsV(next.UseWindowsV);
        shelf?.ApplyAppearance(next.Appearance);
        if (!UpdateSelectionCapture()) return "Impostazioni salvate, ma la copia alla selezione non è disponibile. Riprova dopo il riavvio.";
        if (!windowsVAvailable) return "Impostazioni salvate. Win+V non disponibile: usa la scorciatoia alternativa.";
        if (!alternativeAvailable) return "Win+V restituito a Windows. Scorciatoia alternativa già in uso.";
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
    void OnCopied() { if (!shuttingDown && settings.CopySound && history?.IsPaused == false) CopyFeedbackSound.Play(settings.CopySoundVolume); }
    bool UpdateSelectionCapture()
    {
        if (selectionCopy is null) return true;
        if (settings.CopyOnSelection && storageReady && history?.IsPaused == false) return selectionCopy.Start();
        selectionCopy.Stop(); return true;
    }
    void Shutdown()
    {
        if (shuttingDown) return; shuttingDown = true;
        CopyFeedbackSound.Stop();
        selectionCopy?.Dispose(); foreground?.Dispose(); monitor?.Dispose(); clipboard?.Dispose(); hotkey?.Dispose(); tray?.Dispose(); messages?.Dispose(); settingsWindow?.Close(); shelf?.ExitWindow(); instance.Dispose(); Exit();
    }
}
