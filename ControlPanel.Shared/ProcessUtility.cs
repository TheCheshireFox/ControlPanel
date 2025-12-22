using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ControlPanel.Shared;

public static class ProcessUtility
{
    public static string? GetBinaryPath(int pid)
    {
        if (pid <= 0)
            return null;

        return OperatingSystem.IsWindows() ? GetBinaryPathWin(pid) : GetBinaryPathLinux(pid);
    }

    private static string? GetBinaryPathLinux(int pid)
    {
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
    
    private static string? GetBinaryPathWin(int pid)
    {
        using var hProcess = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, pid);
        if (hProcess.IsInvalid)
            return null;

        var size = 260;
        var sb = new StringBuilder(size);

        while (true)
        {
            if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                return sb.ToString(0, size);

            if (Marshal.GetLastWin32Error() != 122) // ERROR_INSUFFICIENT_BUFFER
                return null; 
            
            sb.Capacity *= 2;
            size = sb.Capacity;
        }
    }

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        QueryLimitedInformation = 0x00001000
    }
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]  
    private static extern bool QueryFullProcessImageName(SafeProcessHandle hProcess, int dwFlags, StringBuilder? lpExeName, ref int lpdwSize);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);
}