using System.Text;
using System.Text.Json;
namespace ClipShelf.Core.History;
public static class FileReferences
{
    public const int MaxItems = 1000;
    public static bool Valid(IReadOnlyList<ClipFile>? files)
    {
        if (files is not { Count: > 0 and <= MaxItems }) return false;
        try { return files.All(file => !string.IsNullOrWhiteSpace(file.Path) && Path.IsPathFullyQualified(file.Path) && Path.GetFullPath(file.Path).Length <= 32767); }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (PathTooLongException) { return false; }
    }
    public static byte[] Identity(IReadOnlyList<ClipFile> files)
    {
        var canonical = files.Select(file => new ClipFile(Path.GetFullPath(file.Path), file.IsDirectory))
            .Distinct().OrderBy(file => file.Path, StringComparer.Ordinal).ThenBy(file => file.IsDirectory).ToArray();
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical));
    }
    public static string Display(IReadOnlyList<ClipFile> files) =>
        string.Join(Environment.NewLine, files.Take(4).Select(file => (file.IsDirectory ? "📁 " : "📄 ") + Path.GetFileName(file.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))))
        + (files.Count > 4 ? $"{Environment.NewLine}+ altri {files.Count - 4}" : "");
    public static bool Available(IReadOnlyList<ClipFile> files) => files.All(file => file.IsDirectory ? Directory.Exists(file.Path) : File.Exists(file.Path));
}
