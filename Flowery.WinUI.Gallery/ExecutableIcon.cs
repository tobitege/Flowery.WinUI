using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;

namespace Flowery.WinUI.Gallery;

// Shows the icon that ApplicationIcon embeds in the executable, so no icon file has to ship beside it.
internal static partial class ExecutableIcon
{
    // The .NET SDK stores ApplicationIcon as icon group 32512 in the app host.
    private const int ApplicationIconResourceId = 32512;
    private const uint ImageIcon = 1;
    private const uint LoadDefaultSize = 0x40;

    public static void Apply(AppWindow appWindow)
    {
        var icon = LoadImage(GetModuleHandle(null), ApplicationIconResourceId, ImageIcon, 0, 0, LoadDefaultSize);
        if (icon != IntPtr.Zero)
            appWindow.SetIcon(Win32Interop.GetIconIdFromIcon(icon));
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandle(string? moduleName);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true)]
    private static partial IntPtr LoadImage(IntPtr instance, nint name, uint type, int width, int height, uint load);
}
