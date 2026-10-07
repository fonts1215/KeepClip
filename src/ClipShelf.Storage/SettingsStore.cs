using System.Text.Json;
using ClipShelf.Core;
namespace ClipShelf.Storage;
public sealed class SettingsStore(string directory)
{
    string FilePath => Path.Combine(directory, "settings.json");
    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(FilePath)) return AppSettings.Default;
        var value = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(FilePath));
        return value is not null && value.Hotkey is not null ? value : AppSettings.Default;
    }
    public async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(directory); var temp = FilePath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}
