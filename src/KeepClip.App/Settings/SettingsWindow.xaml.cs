using KeepClip.App.Desktop;
using KeepClip.App.Clipboard;
using KeepClip.App.Appearance;
using KeepClip.Application.Appearance;
using KeepClip.Application;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
using Windows.Graphics;
namespace KeepClip.App.Settings;
internal sealed partial class SettingsWindow : Window
{
    readonly Func<AppSettings, Task<string?>> apply; readonly Func<Task> recover;
    readonly WindowTheme theme;
    readonly string dataPath;
    string selectedPalette = "coral";
    bool appearanceReady;
    public SettingsWindow(AppSettings settings, string dataPath, Func<AppSettings, Task<string?>> apply, Func<Task> recover)
    {
        InitializeComponent(); this.apply = apply; this.recover = recover; this.dataPath = dataPath;
        theme = new(this, Root);
        CreatePaletteChoices(); SetAppearance(ThemeCatalog.Normalize(settings.Appearance));
        appearanceReady = true; PreviewAppearance();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "KeepClip.ico"));
        WindowsVShortcut.IsOn = settings.UseWindowsV;
        Modifiers.SelectedIndex = settings.Hotkey.Modifiers switch { 3 => 1, 5 => 2, _ => 0 };
        Letter.Text = ((char)settings.Hotkey.Key).ToString(); Pause.IsOn = !settings.IsPaused;
        CopySelection.IsOn = settings.CopyOnSelection; CopySound.IsOn = settings.CopySound;
        SoundVolume.Value = Math.Clamp(settings.CopySoundVolume, 0, 100);
        SoundVolumeLabel.Text = $"{(int)SoundVolume.Value}%";
        DataPathLabel.Text = "Dati locali: " + dataPath;
        AppWindow.Resize(new SizeInt32(680, 690)); ((OverlappedPresenter)AppWindow.Presenter).IsMaximizable = false;
        Root.Loaded += async (_, _) => await RefreshStartupAsync();
    }
    async void Save_Click(object sender, RoutedEventArgs e)
    {
        var key = Letter.Text.Trim().ToUpperInvariant();
        if (key.Length != 1 || key[0] is < 'A' or > 'Z') { Show("Inserisci una lettera da A a Z."); return; }
        SaveButton.IsEnabled = false;
        try
        {
            var settings = new AppSettings(new Hotkey(Modifiers.SelectedIndex switch { 1 => 3u, 2 => 5u, _ => 6u }, key[0]), !Pause.IsOn, AutoStart.IsOn, CopySelection.IsOn, CopySound.IsOn, (int)Math.Round(SoundVolume.Value), ReadAppearance()) { UseWindowsV = WindowsVShortcut.IsOn };
            Show(await apply(settings) ?? "Impostazioni salvate."); await RefreshStartupAsync();
        }
        catch { Show("Impossibile salvare le impostazioni."); }
        finally { SaveButton.IsEnabled = true; }
    }
    void CreatePaletteChoices()
    {
        for (var i = 0; i < ThemeCatalog.Palettes.Count; i++)
        {
            var palette = ThemeCatalog.Palettes[i];
            var content = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = new SolidColorBrush(WindowTheme.Color(palette.Accent)) });
            content.Children.Add(new TextBlock { Text = palette.Name, FontSize = 11, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            var button = new ToggleButton { Tag = palette.Id, Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Padding = new Thickness(8) };
            AutomationProperties.SetName(button, palette.Name + ", palette del tema");
            ToolTipService.SetToolTip(button, palette.Name);
            button.Click += (_, _) => { selectedPalette = palette.Id; PreviewAppearance(); };
            Grid.SetRow(button, i / 4); Grid.SetColumn(button, i % 4); PaletteChoices.Children.Add(button);
        }
    }
    AppearanceSettings ReadAppearance() => new((ThemeMode)Math.Max(0, ThemeChoice.SelectedIndex), selectedPalette, ColorSurfaces.IsChecked == true);
    void SetAppearance(AppearanceSettings appearance)
    {
        selectedPalette = appearance.Palette; ThemeChoice.SelectedIndex = (int)appearance.Mode;
        ColorSurfaces.IsChecked = appearance.ColorSurfaces;
    }
    void Appearance_Changed(object sender, RoutedEventArgs args) { if (appearanceReady) PreviewAppearance(); }
    void ResetTheme_Click(object sender, RoutedEventArgs args) { SetAppearance(new()); PreviewAppearance(); }
    void PreviewAppearance()
    {
        foreach (var button in PaletteChoices.Children.OfType<ToggleButton>()) button.IsChecked = (string)button.Tag == selectedPalette;
        theme.Apply(ReadAppearance());
    }
    void SoundVolume_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (SoundVolumeLabel is not null) SoundVolumeLabel.Text = $"{(int)Math.Round(e.NewValue)}%";
    }
    void PreviewSound_Click(object sender, RoutedEventArgs e)
    {
        var volume = (int)Math.Round(SoundVolume.Value);
        if (volume == 0) { CopyFeedbackSound.Stop(); Show("Volume a 0%: il suono è silenzioso."); }
        else if (!CopyFeedbackSound.Play(volume)) Show("Impossibile riprodurre il suono di prova.");
    }
    async void ExportLogs_Click(object sender, RoutedEventArgs e)
    {
        ExportLogsButton.IsEnabled = false;
        try
        {
            var report = await Task.Run(() => DiagnosticLog.ReadForExport(dataPath));
            if (report is null)
            {
                Show("Non ci sono ancora log da esportare.");
                return;
            }

            var picker = new Microsoft.Windows.Storage.Pickers.FileSavePicker(AppWindow.Id)
            {
                SuggestedFileName = $"KeepClip-log-{DateTime.Now:yyyy-MM-dd-HHmmss}"
            };
            picker.FileTypeChoices.Add("File di testo", [".txt"]);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            if (!string.Equals(Path.GetExtension(file.Path), ".txt", StringComparison.OrdinalIgnoreCase))
            {
                Show("Scegli un nome con estensione .txt.");
                return;
            }
            await File.WriteAllTextAsync(file.Path, report);
            Show("Log esportato. Puoi allegare il file alla segnalazione.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            Show("Impossibile esportare il log. Controlla la cartella e riprova.");
        }
        finally { ExportLogsButton.IsEnabled = true; }
    }

    async void Recover_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme, Title = "Ripristinare la cronologia?", Content = "Verrà creata una copia di backup completa della cronologia e delle immagini prima di iniziare una cronologia vuota.", PrimaryButtonText = "Ripristina", CloseButtonText = "Annulla", DefaultButton = ContentDialogButton.Close };
        theme.StyleDialog(dialog);
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
