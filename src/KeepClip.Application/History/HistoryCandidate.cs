using KeepClip.Domain.History;
using System.Security.Cryptography;
using System.Text;

namespace KeepClip.Application.History;

internal sealed class HistoryCandidate
{
    readonly ClipContent content;
    readonly byte[] identityBytes;

    HistoryCandidate(ClipContent content, byte[] identityBytes)
    {
        this.content = content;
        this.identityBytes = identityBytes;
    }

    public byte[]? ImageBytes => content.Kind == ClipKind.Image ? identityBytes : null;

    public static (HistoryCandidate? Candidate, string? Rejection) Prepare(ClipContent content)
    {
        switch (content.Kind)
        {
            case ClipKind.Text when !string.IsNullOrEmpty(content.Text):
                return (new(content, Encoding.UTF8.GetBytes(content.Text)), null);
            case ClipKind.Image when content.Png is { Length: > 0 } png:
                return (new(content, png), null);
            case ClipKind.Files:
                return FileReferences.Valid(content.Files)
                    ? (new(content, FileReferences.Identity(content.Files!)), null)
                    : (null, "Selezione di file non valida: sono supportati fino a 1000 file o cartelle con percorso disponibile.");
            default:
                return (null, null);
        }
    }

    public ClipEntry CreateEntry(Guid id, DateTimeOffset copiedAt, ClipSource? source)
    {
        var hash = Convert.ToHexString(SHA256.HashData(identityBytes));
        return new(id, content.Kind, hash, copiedAt, false,
            content.Kind == ClipKind.Text ? content.Text : null, null, identityBytes.LongLength, source,
            content.Kind == ClipKind.Files ? content.Files!.ToArray() : null);
    }
}
