using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using KeepClip.App.Clipboard;
using KeepClip.Core;
using KeepClip.Core.History;

namespace KeepClip.Tests;

// All clipboard and memory calls use a fake. These tests never access OS clipboard data.
public class NativeFileClipboardTests
{
    static ClipFile[] Files => [new(@"C:\Files\KeepClip.dll"), new(@"C:\Files\Résumé 📎.pdf"), new(@"C:\Files\Folder", true)];
    const string Token = "owner:operation";

    [Fact] public async Task PublishesNativeUnicodeFileListCopyEffectAndPrivateTokenTogether()
    {
        var api = new FakeApi(); var clipboard = new NativeFileClipboard(() => 42, api);
        await clipboard.WriteAsync(Files, Token, 10, () => true, default);
        var payload = api.Published[15];
        Assert.Equal(20u, BinaryPrimitives.ReadUInt32LittleEndian(payload));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(16)));
        Assert.Equal(string.Join('\0', Files.Select(file => file.Path)) + "\0\0", Encoding.Unicode.GetString(payload, 20, payload.Length - 20));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(api.Published[api.Formats["Preferred DropEffect"]]));
        Assert.Equal(42, api.WriteOwner); Assert.Equal(0, api.LiveAllocations);
        Assert.Equal(Token, clipboard.ReadToken());
    }

    [Fact] public async Task BusyClipboardRetriesWithoutLosingFormatsOrAllocations()
    {
        var api = new FakeApi { BusyAttempts = 2 }; var clipboard = new NativeFileClipboard(() => 42, api);
        await clipboard.WriteAsync(Files, Token, 10, () => true, default);
        Assert.Equal(3, api.OpenAttempts); Assert.Equal(1, api.Empties);
        Assert.Equal(3, api.Published.Count); Assert.Equal(0, api.LiveAllocations);
    }

    [Fact] public async Task NewCopyDuringOpenCannotBeOverwritten()
    {
        var api = new FakeApi { OnOpen = current => current.Sequence++ };
        await Assert.ThrowsAsync<ClipboardWriteSupersededException>(() => new NativeFileClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => true, default));
        Assert.Equal(0, api.Empties); Assert.Empty(api.Published); Assert.Equal(0, api.LiveAllocations); Assert.False(api.IsOpen);
    }

    [Fact] public async Task CancelledSessionBeforeCommitPreservesClipboard()
    {
        var api = new FakeApi();
        await Assert.ThrowsAsync<ClipboardRequestCancelledException>(() => new NativeFileClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => false, default));
        Assert.Equal(0, api.Empties); Assert.Empty(api.Published); Assert.Equal(0, api.LiveAllocations);
    }

    [Fact] public async Task CancellationDuringOpenReleasesMemoryAndClosesClipboard()
    {
        using var cancellation = new CancellationTokenSource();
        var api = new FakeApi { OnOpen = _ => cancellation.Cancel() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new NativeFileClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => true, cancellation.Token));
        Assert.Equal(0, api.Empties); Assert.Empty(api.Published); Assert.Equal(0, api.LiveAllocations); Assert.False(api.IsOpen);
    }

    [Fact] public async Task PersistentBusyClipboardDoesNotEraseExistingData()
    {
        var api = new FakeApi { BusyAttempts = 20 };
        await Assert.ThrowsAsync<COMException>(() => new NativeFileClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => true, default));
        Assert.Equal(4, api.OpenAttempts); Assert.Equal(0, api.Empties); Assert.Equal(0, api.LiveAllocations);
    }

    [Fact] public async Task AllocationFailureLeavesClipboardUntouched()
    {
        var api = new FakeApi { FailAllocation = 2 };
        await Assert.ThrowsAsync<OutOfMemoryException>(() => new NativeFileClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => true, default));
        Assert.Equal(0, api.OpenAttempts); Assert.Equal(0, api.LiveAllocations);
    }

    [Fact] public async Task FailedPublicationDoesNotExposePartialFileSelection()
    {
        var api = new FakeApi { FailFormat = 15 };
        await Assert.ThrowsAsync<Win32Exception>(() => new NativeFileClipboard(() => 42, api).WriteAsync(Files, Token, 10, () => true, default));
        Assert.Empty(api.Published); Assert.Equal(0, api.LiveAllocations); Assert.False(api.IsOpen);
    }

    [Fact] public async Task MissingOwnerAndInvalidSelectionDoNotTouchClipboard()
    {
        var api = new FakeApi();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new NativeFileClipboard(() => 0, api).WriteAsync(Files, Token, 10, () => true, default));
        await Assert.ThrowsAsync<InvalidDataException>(() => new NativeFileClipboard(() => 42, api).WriteAsync([], Token, 10, () => true, default));
        Assert.Equal(0, api.OpenAttempts); Assert.Equal(0, api.Empties); Assert.Equal(0, api.LiveAllocations);
    }

    [Fact] public void MalformedOrOversizedPrivateTokenIsIgnored()
    {
        var api = new FakeApi(); var clipboard = new NativeFileClipboard(() => 42, api);
        var format = api.Formats[NativeFileClipboard.OwnerFormat];
        api.Published[format] = [1]; Assert.Null(clipboard.ReadToken());
        api.Published[format] = new byte[4096]; Assert.Null(clipboard.ReadToken());
        api.Published[format] = Encoding.Unicode.GetBytes("unterminated"); Assert.Null(clipboard.ReadToken());
        Assert.False(api.IsOpen);
    }

    [Fact] public void NativeAllocationPaddingDoesNotHideOwnWriteMarker()
    {
        var api = new FakeApi(); var clipboard = new NativeFileClipboard(() => 42, api);
        api.Published[api.Formats[NativeFileClipboard.OwnerFormat]] = Encoding.Unicode.GetBytes(Token + "\0\0\0");
        Assert.Equal(Token, clipboard.ReadToken());
    }

    sealed class FakeApi : INativeFileClipboardApi
    {
        public Dictionary<string, uint> Formats = new();
        public Dictionary<uint, byte[]> Published = new();
        readonly Dictionary<nint, byte[]> allocations = new();
        int allocationCount;
        public uint Sequence { get; set; } = 10;
        public int BusyAttempts, OpenAttempts, Empties, FailAllocation;
        public uint FailFormat;
        public bool IsOpen;
        public nint WriteOwner;
        public Action<FakeApi>? OnOpen;
        public int LiveAllocations => allocations.Count;
        public uint RegisterFormat(string name) { if (!Formats.TryGetValue(name, out var value)) Formats[name] = value = (uint)(49152 + Formats.Count); return value; }
        public bool Contains(uint format) => Published.ContainsKey(format);
        public bool Open(nint owner) { OpenAttempts++; if (BusyAttempts-- > 0) return false; IsOpen = true; WriteOwner = owner; OnOpen?.Invoke(this); return true; }
        public void Close() => IsOpen = false;
        public bool Empty() { Assert.True(IsOpen); Empties++; Published.Clear(); Sequence++; return true; }
        public nint Allocate(byte[] bytes) { if (++allocationCount == FailAllocation) throw new OutOfMemoryException(); var handle = (nint)allocationCount; allocations.Add(handle, bytes); return handle; }
        public void Free(nint handle) { Assert.True(allocations.Remove(handle)); }
        public bool Publish(uint format, nint handle) { Assert.True(IsOpen); if (format == FailFormat) return false; Published[format] = allocations[handle]; allocations.Remove(handle); return true; }
        public byte[]? Read(uint format, int maxBytes) { Assert.True(IsOpen); return Published.TryGetValue(format, out var bytes) && bytes.Length <= maxBytes ? bytes : null; }
    }
}
