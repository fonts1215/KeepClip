using KeepClip.Core.History;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
namespace KeepClip.App.Shelf;
public sealed partial class ClipCard : UserControl
{
    public ClipCard() { InitializeComponent(); DataContextChanged += (_, _) => Refresh(); }
    async void Refresh()
    {
        PreviewImage.Source = null; PreviewText.Text = ""; SourceIcon.Source = null; SourceFallback.Visibility = Visibility.Visible;
        KindLabel.Text = ""; TimeLabel.Text = ""; SourceName.Text = ""; PinLabel.Visibility = Visibility.Collapsed;
        if (DataContext is not ClipEntry entry) return;
        KindLabel.Text = entry.Kind switch { ClipKind.Text => "TESTO", ClipKind.Image => "IMMAGINE", ClipKind.Files => (entry.Files?.Count ?? 0) + " FILE", _ => "" };
        PinLabel.Visibility = entry.IsPinned ? Visibility.Visible : Visibility.Collapsed;
        TimeLabel.Text = entry.CopiedAt.ToLocalTime().ToString("dd MMM · HH:mm");
        PreviewText.Text = entry.Text is { Length: > 1200 } text ? text[..1200] : entry.Text ?? "";
        if (entry.Kind == ClipKind.Files && entry.Files is { } files) PreviewText.Text = FileReferences.Display(files);
        SourceName.Text = entry.Source?.Name ?? "Origine sconosciuta";
        ToolTipService.SetToolTip(SourceName, SourceName.Text);
        if (entry.Source?.IconPng is { Length: > 0 } bytes)
        {
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); writer.DetachStream(); }
                stream.Seek(0); var icon = new BitmapImage { DecodePixelWidth = 80 }; await icon.SetSourceAsync(stream);
                if (!ReferenceEquals(DataContext, entry)) return;
                SourceIcon.Source = icon; SourceFallback.Visibility = Visibility.Collapsed;
            }
            catch { }
        }
        if (!ReferenceEquals(DataContext, entry)) return;
        if (entry.Kind == ClipKind.Files && entry.Files is { } references)
        {
            var available = await Task.Run(() => FileReferences.Available(references));
            if (ReferenceEquals(DataContext, entry) && !available) PreviewText.Text = "Originali non disponibili" + Environment.NewLine + FileReferences.Display(references);
        }
        if (!ReferenceEquals(DataContext, entry)) return;
        if (entry.ImagePath is not null)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(entry.ImagePath); using var stream = await file.OpenReadAsync();
                var image = new BitmapImage { DecodePixelWidth = 440 }; await image.SetSourceAsync(stream);
                if (ReferenceEquals(DataContext, entry)) PreviewImage.Source = image;
            }
            catch { if (ReferenceEquals(DataContext, entry)) PreviewText.Text = "Anteprima non disponibile"; }
        }
    }
}
