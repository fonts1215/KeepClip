using System.Runtime.InteropServices;
using KeepClip.App.Interop;
namespace KeepClip.Tests;
public class NativeInputLayoutTests
{
    [Fact] public void MouseHookDataMatchesWindowsX64Abi()
    {
        Assert.Equal(32, Marshal.SizeOf<NativeMethods.MouseHookData>());
        Assert.Equal((nint)24, Marshal.OffsetOf<NativeMethods.MouseHookData>(nameof(NativeMethods.MouseHookData.Extra)));
    }
    [Fact] public void InputMatchesWindowsX64Abi()
    {
        Assert.Equal(8, IntPtr.Size);
        Assert.Equal(40, Marshal.SizeOf<NativeMethods.Input>());
        Assert.Equal((nint)8, Marshal.OffsetOf<NativeMethods.Input>(nameof(NativeMethods.Input.Data)));
        Assert.Equal((nint)16, Marshal.OffsetOf<NativeMethods.KeyboardInput>(nameof(NativeMethods.KeyboardInput.Extra)));
    }
}
