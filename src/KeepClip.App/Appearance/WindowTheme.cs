using KeepClip.Application.Appearance;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.System;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace KeepClip.App.Appearance;

internal sealed class WindowTheme : IDisposable
{
    readonly Window window;
    readonly FrameworkElement root;
    readonly UISettings ui = new();
    readonly ThemeSettings themeSettings;
    AppearanceSettings settings = new();
    bool disposed;

    public WindowTheme(Window window, FrameworkElement root)
    {
        this.window = window; this.root = root;
        // AccessibilitySettings.HighContrastChanged requires UWP UI infrastructure.
        // ThemeSettings is the Windows App SDK equivalent for Win32/WinUI windows.
        themeSettings = ThemeSettings.CreateForWindowId(window.AppWindow.Id);
        ui.ColorValuesChanged += ColorsChanged;
        themeSettings.Changed += ContrastChanged;
        root.Loaded += RootLoaded;
        window.Closed += (_, _) => Dispose();
    }
    public void Apply(AppearanceSettings? next)
    {
        settings = ThemeCatalog.Normalize(next);
        Refresh();
    }
    public void StyleDialog(ContentDialog dialog)
    {
        // Popups do not inherit the window root's resource scope.
        foreach (var resource in root.Resources) dialog.Resources[resource.Key] = resource.Value;
        dialog.SetBinding(FrameworkElement.RequestedThemeProperty,
            new Binding { Source = root, Path = new PropertyPath(nameof(FrameworkElement.RequestedTheme)), Mode = BindingMode.OneWay });
    }
    void RootLoaded(object sender, RoutedEventArgs args) => Refresh();
    void ColorsChanged(UISettings sender, object args) => QueueRefresh();
    void ContrastChanged(ThemeSettings sender, object args) => QueueRefresh();
    void QueueRefresh() => root.DispatcherQueue.TryEnqueue(() => { if (!disposed) Refresh(); });
    void Refresh()
    {
        if (disposed) return;
        var systemBackground = ui.GetColorValue(UIColorType.Background);
        var colors = ThemeCatalog.Resolve(settings, systemBackground.R + systemBackground.G + systemBackground.B < 384);
        root.RequestedTheme = colors.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        if (themeSettings.HighContrast)
        {
            var page = Pack(ui.UIElementColor(UIElementType.Window));
            var text = Pack(ui.UIElementColor(UIElementType.WindowText));
            var accent = Pack(ui.UIElementColor(UIElementType.Highlight));
            var onAccent = Pack(ui.UIElementColor(UIElementType.HighlightText));
            colors = colors with { Background = page, Surface = page, Header = page, Text = text, SecondaryText = text,
                HeaderText = text, Border = text, Accent = accent, AccentText = text, AccentForeground = onAccent,
                AccentHover = accent, AccentPressed = accent, Selection = page };
        }
        Set("KeepClipPageBrush", colors.Background); Set("KeepClipSurfaceBrush", colors.Surface);
        Set("KeepClipHeaderBrush", colors.Header); Set("KeepClipTextBrush", colors.Text);
        Set("KeepClipSecondaryTextBrush", colors.SecondaryText); Set("KeepClipHeaderTextBrush", colors.HeaderText);
        Set("KeepClipBorderBrush", colors.Border); Set("KeepClipAccentBrush", colors.Accent);
        Set("KeepClipAccentTextBrush", colors.AccentText); Set("KeepClipAccentForegroundBrush", colors.AccentForeground);
        Set("KeepClipAccentHoverBrush", colors.AccentHover); Set("KeepClipAccentPressedBrush", colors.AccentPressed);
        Set("KeepClipSelectionBrush", colors.Selection);
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var bar = window.AppWindow.TitleBar;
            bar.BackgroundColor = bar.InactiveBackgroundColor = bar.ButtonBackgroundColor = bar.ButtonInactiveBackgroundColor = Color(colors.Background);
            bar.ForegroundColor = bar.ButtonForegroundColor = Color(colors.Text);
            bar.InactiveForegroundColor = bar.ButtonInactiveForegroundColor = Color(colors.SecondaryText);
            bar.ButtonHoverBackgroundColor = Color(colors.Surface); bar.ButtonHoverForegroundColor = Color(colors.Text);
        }
    }
    void Set(string key, uint value) => ((SolidColorBrush)root.Resources[key]).Color = Color(value);
    internal static Color Color(uint value) => Windows.UI.Color.FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    static uint Pack(Color color) => (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B);
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        ui.ColorValuesChanged -= ColorsChanged; themeSettings.Changed -= ContrastChanged;
        root.Loaded -= RootLoaded;
    }
}
