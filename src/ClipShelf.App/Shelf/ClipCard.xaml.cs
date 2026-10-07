using ClipShelf.Core.History;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
namespace ClipShelf.App.Shelf;
public sealed partial class ClipCard : UserControl
{
    public ClipCard() { InitializeComponent(); DataContextChanged += (_, _) => Refresh(); }
    async void Refresh()
    {
        PreviewImage.Source = null; PreviewText.Text = "";
        if (DataContext is not ClipEntry entry) return;
        KindLabel.Text = entry.Kind == ClipKind.Text ? "TESTO" : "IMMAGINE";
        PinLabel.Visibility = entry.IsPinned ? Visibility.Visible : Visibility.Collapsed;
        TimeLabel.Text = entry.CopiedAt.ToLocalTime().ToString("dd MMM · HH:mm");
        PreviewText.Text = entry.Text is { Length: > 1200 } text ? text[..1200] : entry.Text ?? "";
        if (entry.ImagePath is not null)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(entry.ImagePath); using var stream = await file.OpenReadAsync();
                var image = new BitmapImage { DecodePixelWidth = 440 }; await image.SetSourceAsync(stream);
                if (DataContext is ClipEntry current && current.Id == entry.Id) PreviewImage.Source = image;
            }
            catch { if (DataContext is ClipEntry current && current.Id == entry.Id) PreviewText.Text = "Anteprima non disponibile"; }
        }
    }
}
