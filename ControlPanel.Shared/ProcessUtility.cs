using System.Diagnostics;

namespace ControlPanel.Shared;

public static class ProcessUtility
{
    public static string? GetBinaryPath(int pid)
    {
        if (pid <= 0)
            return null;

        try
        {
            using var process = Process.GetProcessById(pid);
            return process.MainModule?.FileName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}