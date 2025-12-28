using System.Runtime.InteropServices;

namespace ControlPanel.BtRfcomm.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct PollFd
{
    public int Fd;
    public PollEvent Events;
    public PollEvent REvents;
}