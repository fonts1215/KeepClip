namespace KeepClip.App.Desktop;

internal sealed class WindowsVGesture
{
    bool vDown, swallowed;

    public void Reset(bool vAlreadyDown)
    {
        vDown = vAlreadyDown;
        swallowed = false;
    }

    public bool Handle(bool down, bool windowsOnly, Func<bool> invoke)
    {
        if (!down)
        {
            var swallowRelease = swallowed;
            Reset(vAlreadyDown: false);
            return swallowRelease;
        }

        if (vDown) return swallowed;
        vDown = true;
        swallowed = windowsOnly && invoke();
        return swallowed;
    }
}
