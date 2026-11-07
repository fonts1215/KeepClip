using KeepClip.App.Interop;
using KeepClip.App.Appearance;
using KeepClip.Application.Appearance;
using KeepClip.Application;
using KeepClip.Domain.History;
using KeepClip.Application.Paste;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
namespace KeepClip.App.Shelf;
public sealed partial class ShelfWindow : Window
{
    readonly HistoryService history; readonly ShelfViewModel model = new();
    readonly PasteClip paste;
    readonly PasteSession session = new();
    readonly OutsideClickWatcher outsideClicks;
    readonly WindowTheme theme;
    PasteRequest? continuation;
    bool busy, dialogOpen, exiting;
    NativeMethods.Rect workArea;
    double displayScale = 1;
    int shelfWidth;
    public bool IsVisible { get; private set; }
    public event Action? SettingsRequested;
    public event Action<string>? Notification;
    public ShelfWindow(HistoryService history, IClipboardAdapter clipboard, IPasteContentLoader contentLoader)
    {
        try { InitializeComponent(); }
        catch (Exception ex) { DiagnosticLog.StartupFailure("shelf-xaml", ex); throw; }
        DiagnosticLog.Write("shelf-start stage=xaml-loaded"); this.history = history; Root.DataContext = model;
        theme = new(this, Root);
        DiagnosticLog.Write("shelf-start stage=theme-created");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "KeepClip.ico"));
        outsideClicks = new(WinRT.Interop.WindowNative.GetWindowHandle(this), () => IsVisible && !dialogOpen, () =>
        {
            var request = session.Snapshot();
            DispatcherQueue.TryEnqueue(() => { if (session.IsCurrent(request) && IsVisible && !dialogOpen) { DiagnosticLog.Write("shelf-dismiss outside-click"); Hide(); } });
        });
        paste = new(new PasteCoordinator(clipboard, new WindowTarget(), new PasteInput(), () => { Hide(); continuation = session.Snapshot(); }), contentLoader);
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(true, false); presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false; presenter.IsAlwaysOnTop = true;
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += (_, args) => { if (!exiting) { args.Cancel = true; Hide(); } };
        Activated += (_, args) => { if (args.WindowActivationState == WindowActivationState.Deactivated && !dialogOpen) Hide(); };
        history.HistoryChanged += OnHistoryChanged;
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(model.Entries)) UpdateCount(); };
        OnHistoryChanged();
        Status.RegisterPropertyChangedCallback(InfoBar.IsOpenProperty, (_, _) => ResizeShelf());
        Status.SizeChanged += (_, _) => ResizeShelf();
    }
    public void ApplyAppearance(AppearanceSettings? appearance) => theme.Apply(appearance);
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
            workArea = info.Work; displayScale = scale; shelfWidth = w;
            ResizeShelf();
            CountLabel.Visibility = w / scale < 1000 ? Visibility.Collapsed : Visibility.Visible;
        }
        IsVisible = true; outsideClicks.Start(); AppWindow.Show(); Activate(); Search.Focus(FocusState.Programmatic);
    }
    void ResizeShelf()
    {
        if (shelfWidth <= 0) return;
        // Measure all rows at their required height. An open InfoBar must add
        // window space rather than taking space away from the cards.
        Root.InvalidateMeasure();
        Root.Measure(new Windows.Foundation.Size(shelfWidth / displayScale, double.PositiveInfinity));
        var height = Math.Min(workArea.Bottom - workArea.Top - 32, (int)Math.Ceiling((Root.DesiredSize.Height + 8) * displayScale));
        var bounds = new RectInt32(workArea.Left + (workArea.Right - workArea.Left - shelfWidth) / 2, workArea.Bottom - height - 16, shelfWidth, height);
        if (AppWindow.Size.Width != bounds.Width || AppWindow.Size.Height != bounds.Height || AppWindow.Position.Y != bounds.Y)
            AppWindow.MoveAndResize(bounds);
    }
    public void Hide() { if (!IsVisible) return; session.Cancel(); IsVisible = false; outsideClicks.Stop(); AppWindow.Hide(); }
    public void ShowMessage(string message) { Status.Message = message; Status.IsOpen = true; }
    public void ExitWindow() { exiting = true; outsideClicks.Dispose(); history.HistoryChanged -= OnHistoryChanged; Close(); }
    void Settings_Click(object sender, RoutedEventArgs e) { Hide(); SettingsRequested?.Invoke(); }
    async void Card_PasteRequested(object? sender, ClipCardEventArgs e) { model.SelectedEntry = e.Entry; await PasteAsync(e.Entry); }
    async Task PasteAsync(ClipEntry entry)
    {
        if (busy) return; busy = true;
        var request = session.Snapshot();
        try
        {
            continuation = request;
            var result = await paste.ExecuteAsync(entry, request.Destination,
                () => continuation is { } current && session.IsCurrent(current));
            if (result is null) return;
            if (result.Failure == PasteFailure.OriginalFilesUnavailable)
            {
                ShowMessage("Uno o più file originali non sono più disponibili. Ripristinali oppure rimuovi questa card.");
                return;
            }
            DiagnosticLog.Write($"paste kind={entry.Kind} outcome={result.Outcome} failure={result.Failure}");
            if (result.Message is not null) Notification?.Invoke(result.Message);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"paste-error kind={entry.Kind} type={ex.GetType().Name} hresult=0x{ex.HResult:X8}");
            ShowMessage("Impossibile copiare questo elemento. Verifica che il contenuto sia ancora disponibile."); Notification?.Invoke("Impossibile copiare l'elemento selezionato.");
        }
        finally { continuation = null; busy = false; }
    }
    async Task MutateAsync(Func<Task> action)
    {
        if (busy) return; busy = true;
        try { await action(); } catch { ShowMessage("Impossibile salvare la modifica. La cronologia è rimasta invariata."); } finally { busy = false; }
    }
    async void Card_PinRequested(object? sender, ClipCardEventArgs e) => await MutateAsync(() => history.TogglePinAsync(e.Entry.Id));
    async void Card_DeleteRequested(object? sender, ClipCardEventArgs e) => await MutateAsync(() => history.DeleteAsync(e.Entry.Id));
    async void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (dialogOpen || busy) return; dialogOpen = true;
        try
        {
            var include = new CheckBox { Content = "Elimina anche i preferiti" };
            var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme, Title = "Svuotare la cronologia?", Content = include, PrimaryButtonText = "Svuota", CloseButtonText = "Annulla", DefaultButton = ContentDialogButton.Close };
            theme.StyleDialog(dialog);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) await MutateAsync(() => history.ClearAsync(include.IsChecked == true));
        }
        finally { dialogOpen = false; }
    }
    async void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (dialogOpen) return;
        if (e.Key == VirtualKey.Escape) { Hide(); e.Handled = true; }
        // Enter/Space on a card action must remain an action, not a paste.
        else if (FocusManager.GetFocusedElement(Root.XamlRoot) is ButtonBase) return;
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
