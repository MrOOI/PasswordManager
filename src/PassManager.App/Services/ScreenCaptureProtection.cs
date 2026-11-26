using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PassManager.App.Services;

public interface IScreenCaptureProtection
{
    /// <summary>Includes or excludes the window from screenshots, recordings and screen sharing.</summary>
    void Apply(Window window, bool hideFromCapture);
}

public sealed partial class ScreenCaptureProtection : IScreenCaptureProtection
{
    private const uint WdaNone = 0x00;
    private const uint WdaMonitor = 0x01;
    private const uint WdaExcludeFromCapture = 0x11; // Windows 10 version 2004+

    public void Apply(Window window, bool hideFromCapture)
    {
        ArgumentNullException.ThrowIfNull(window);

        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (!hideFromCapture)
        {
            SetWindowDisplayAffinity(handle, WdaNone);
            return;
        }

        // Older Windows versions only support WDA_MONITOR (window shows up black instead of invisible).
        if (!SetWindowDisplayAffinity(handle, WdaExcludeFromCapture))
        {
            SetWindowDisplayAffinity(handle, WdaMonitor);
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
}
