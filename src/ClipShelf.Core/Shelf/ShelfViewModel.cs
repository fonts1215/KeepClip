using System.ComponentModel;
using ClipShelf.Core.History;
namespace ClipShelf.Core.Shelf;
public sealed class ShelfViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<ClipEntry> Entries { get; private set; } = [];
    IReadOnlyList<ClipEntry> all = [];
    ClipEntry? selected;
    string query = "";
    bool pinned;
    public ClipEntry? SelectedEntry { get => selected; set { selected = value; Notify(nameof(SelectedEntry)); } }
    public string Query { get => query; set { query = value; Filter(); Notify(nameof(Query)); } }
    public bool ShowPinnedOnly { get => pinned; set { pinned = value; Filter(); Notify(nameof(ShowPinnedOnly)); } }
    public void Refresh(IReadOnlyList<ClipEntry> entries) { all = entries; Filter(); }
    void Filter()
    {
        var previous = selected?.Id;
        Entries = all.Where(x => (!pinned || x.IsPinned) && (string.IsNullOrWhiteSpace(query) || ((x.Text?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) || (x.Files?.Any(file => Path.GetFileName(file.Path).Contains(query, StringComparison.OrdinalIgnoreCase)) ?? false)))).ToArray();
        selected = Entries.FirstOrDefault(x => x.Id == previous) ?? Entries.FirstOrDefault();
        Notify(nameof(Entries)); Notify(nameof(SelectedEntry));
    }
    void Notify(string name) => PropertyChanged?.Invoke(this, new(name));
}
