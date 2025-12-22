using System.Runtime.InteropServices;

namespace ControlPanel.Agent.Windows;

internal static partial class ConsoleWindow
{
    private enum ShowWindowCommand
    {
        Hide = 0,
    }

    public static void Hide()
    {
        var hWnd = GetConsoleWindow();
        if (hWnd != IntPtr.Zero)
            ShowWindow(hWnd, (int)ShowWindowCommand.Hide);
    }
    
    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetConsoleWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);
}