using ClipShelf.App.Desktop;
using ClipShelf.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
namespace ClipShelf.App.Settings;
internal sealed partial class SettingsWindow : Window
{
    readonly Func<AppSettings, Task<string?>> apply; readonly Func<Task> recover;
    public SettingsWindow(AppSettings settings, string dataPath, Func<AppSettings, Task<string?>> apply, Func<Task> recover)
    {
        InitializeComponent(); this.apply = apply; this.recover = recover;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "KeepClip.ico"));
        Modifiers.SelectedIndex = settings.Hotkey.Modifiers switch { 3 => 1, 5 => 2, _ => 0 };
        Letter.Text = ((char)settings.Hotkey.Key).ToString(); Pause.IsOn = !settings.IsPaused;
        CopySelection.IsOn = settings.CopyOnSelection; CopySound.IsOn = settings.CopySound;
        DataPathLabel.Text = "Dati locali: " + dataPath;
        AppWindow.Resize(new SizeInt32(600, 690)); ((OverlappedPresenter)AppWindow.Presenter).IsMaximizable = false;
        Root.Loaded += async (_, _) => await RefreshStartupAsync();
    }
    async void Save_Click(object sender, RoutedEventArgs e)
    {
        var key = Letter.Text.Trim().ToUpperInvariant();
        if (key.Length != 1 || key[0] is < 'A' or > 'Z') { Show("Inserisci una lettera da A a Z."); return; }
        SaveButton.IsEnabled = false;
        try
        {
            var settings = new AppSettings(new Hotkey(Modifiers.SelectedIndex switch { 1 => 3u, 2 => 5u, _ => 6u }, key[0]), !Pause.IsOn, AutoStart.IsOn, CopySelection.IsOn, CopySound.IsOn);
            Show(await apply(settings) ?? "Impostazioni salvate."); await RefreshStartupAsync();
        }
        catch { Show("Impossibile salvare le impostazioni."); }
        finally { SaveButton.IsEnabled = true; }
    }
    async void Recover_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Ripristinare la cronologia?", Content = "Verrà creata una copia di backup completa della cronologia e delle immagini prima di iniziare una cronologia vuota.", PrimaryButtonText = "Ripristina", CloseButtonText = "Annulla", DefaultButton = ContentDialogButton.Close };
        try { if (await dialog.ShowAsync() == ContentDialogResult.Primary) { await recover(); Show("Cronologia ripristinata. Il backup è nella cartella dati."); } }
        catch { Show("Ripristino non riuscito. I file disponibili sono stati preservati nella cartella dati."); }
    }
    void Show(string text) { Status.Message = text; Status.IsOpen = true; }
    async Task RefreshStartupAsync()
    {
        var state = await new StartupService().GetAsync(); AutoStart.IsEnabled = state.Available; AutoStart.IsOn = state.Enabled;
        StartupHint.Text = state.Message ?? "L'avvio automatico segue anche le impostazioni di Windows.";
    }
}
