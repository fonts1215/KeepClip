using ClipShelf.App.Interop;
using ClipShelf.Core;
using ClipShelf.Core.History;
using ClipShelf.Core.Paste;
using ClipShelf.Core.Shelf;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
namespace ClipShelf.App.Shelf;
public sealed partial class ShelfWindow : Window
{
    readonly HistoryService history; readonly ShelfViewModel model = new();
    readonly PasteCoordinator paste;
    readonly PasteSession session = new();
    bool busy, dialogOpen, exiting;
    public bool IsVisible { get; private set; }
    public event Action? SettingsRequested;
    public event Action<string>? Notification;
    public ShelfWindow(HistoryService history, IClipboardAdapter clipboard)
    {
        InitializeComponent(); this.history = history; Root.DataContext = model;
        paste = new(clipboard, new WindowTarget(), new PasteInput(), Hide);
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(true, false); presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false; presenter.IsAlwaysOnTop = true;
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += (_, args) => { if (!exiting) { args.Cancel = true; Hide(); } };
        Activated += (_, args) => { if (args.WindowActivationState == WindowActivationState.Deactivated && !dialogOpen) Hide(); };
        history.HistoryChanged += OnHistoryChanged;
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(model.Entries)) UpdateCount(); };
        OnHistoryChanged();
    }
    void OnHistoryChanged() => DispatcherQueue.TryEnqueue(() => { model.Refresh(history.Entries); UpdateCount(); });
    void UpdateCount()
    {
        CountLabel.Text = $"{history.Entries.Count}/200 · Invio incolla · Esc chiude";
        EmptyState.Visibility = model.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = history.Entries.Count == 0 ? "Copia qualcosa per iniziare" : "Nessun elemento trovato";
    }
    public void ShowFor(nint target)
    {
        session.Open(target); model.Query = ""; model.Refresh(history.Entries);
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var monitor = NativeMethods.MonitorFromWindow(target != 0 ? target : hwnd, 2);
        var info = new NativeMethods.MonitorInfo { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            // Move our own per-monitor aware HWND onto the target monitor before reading its DPI.
            AppWindow.Move(new PointInt32(info.Work.Left + 16, info.Work.Top + 16));
            var dpi = NativeMethods.GetDpiForWindow(hwnd); var scale = (dpi == 0 ? 96 : dpi) / 96d;
            var w = Math.Min(info.Work.Right - info.Work.Left - 32, (int)(1200 * scale));
            var h = Math.Min(info.Work.Bottom - info.Work.Top - 32, (int)(370 * scale));
            AppWindow.MoveAndResize(new RectInt32(info.Work.Left + (info.Work.Right - info.Work.Left - w) / 2, info.Work.Bottom - h - 16, w, h));
            CountLabel.Visibility = w / scale < 1000 ? Visibility.Collapsed : Visibility.Visible;
        }
        IsVisible = true; AppWindow.Show(); Activate(); Search.Focus(FocusState.Programmatic);
    }
    public void Hide() { session.Cancel(); IsVisible = false; AppWindow.Hide(); }
    public void ShowMessage(string message) { Status.Message = message; Status.IsOpen = true; }
    public void ExitWindow() { exiting = true; history.HistoryChanged -= OnHistoryChanged; Close(); }
    void Settings_Click(object sender, RoutedEventArgs e) { Hide(); SettingsRequested?.Invoke(); }
    async void Cards_ItemClick(object sender, ItemClickEventArgs e) { if (e.ClickedItem is ClipEntry entry) await PasteAsync(entry); }
    async void Paste_Click(object sender, RoutedEventArgs e) { if (model.SelectedEntry is { } entry) await PasteAsync(entry); }
    async Task PasteAsync(ClipEntry entry)
    {
        if (busy) return; busy = true;
        var request = session.Snapshot();
        try
        {
            ClipContent content;
            if (entry.Kind == ClipKind.Text) content = new(ClipKind.Text, entry.Text);
            else content = new(ClipKind.Image, Png: await File.ReadAllBytesAsync(entry.ImagePath ?? throw new IOException()));
            if (!session.IsCurrent(request)) return;
            var result = await paste.PasteAsync(content, request.Destination);
            if (result.Message is not null) Notification?.Invoke(result.Message);
        }
        catch (Exception) { ShowMessage("Impossibile copiare questo elemento. Verifica che il contenuto sia ancora disponibile."); Notification?.Invoke("Impossibile copiare l'elemento selezionato."); }
        finally { busy = false; }
    }
    async Task MutateAsync(Func<Task> action)
    {
        if (busy) return; busy = true;
        try { await action(); } catch { ShowMessage("Impossibile salvare la modifica. La cronologia è rimasta invariata."); } finally { busy = false; }
    }
    async void Pin_Click(object sender, RoutedEventArgs e) { if (model.SelectedEntry is { } entry) await MutateAsync(() => history.TogglePinAsync(entry.Id)); }
    async void Delete_Click(object sender, RoutedEventArgs e) { if (model.SelectedEntry is { } entry) await MutateAsync(() => history.DeleteAsync(entry.Id)); }
    async void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (dialogOpen || busy) return; dialogOpen = true;
        try
        {
            var include = new CheckBox { Content = "Elimina anche i preferiti" };
            var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Svuotare la cronologia?", Content = include, PrimaryButtonText = "Svuota", CloseButtonText = "Annulla", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) await MutateAsync(() => history.ClearAsync(include.IsChecked == true));
        }
        finally { dialogOpen = false; }
    }
    async void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (dialogOpen) return;
        if (e.Key == VirtualKey.Escape) { Hide(); e.Handled = true; }
        else if (e.Key == VirtualKey.Enter && model.SelectedEntry is { } entry) { e.Handled = true; await PasteAsync(entry); }
        else if (e.Key is VirtualKey.Down or VirtualKey.Up || (e.Key is VirtualKey.Left or VirtualKey.Right && Search.FocusState == FocusState.Unfocused))
        {
            var index = model.Entries.ToList().FindIndex(x => x.Id == model.SelectedEntry?.Id);
            var delta = e.Key is VirtualKey.Left or VirtualKey.Up ? -1 : 1;
            if (model.Entries.Count > 0) { model.SelectedEntry = model.Entries[Math.Clamp(index + delta, 0, model.Entries.Count - 1)]; Cards.ScrollIntoView(model.SelectedEntry); }
            e.Handled = true;
        }
    }
}
