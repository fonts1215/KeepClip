using System.Runtime.InteropServices;
using ClipShelf.App.Interop;
namespace ClipShelf.Tests;
public class NativeInputLayoutTests
{
    [Fact] public void InputMatchesWindowsX64Abi()
    {
        Assert.Equal(8, IntPtr.Size);
        Assert.Equal(40, Marshal.SizeOf<NativeMethods.Input>());
        Assert.Equal((nint)8, Marshal.OffsetOf<NativeMethods.Input>(nameof(NativeMethods.Input.Data)));
        Assert.Equal((nint)16, Marshal.OffsetOf<NativeMethods.KeyboardInput>(nameof(NativeMethods.KeyboardInput.Extra)));
    }
}
