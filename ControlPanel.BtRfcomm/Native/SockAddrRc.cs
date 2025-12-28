using System.Runtime.InteropServices;

namespace ControlPanel.BtRfcomm.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct SockAddrRc
{
    public AddressFamily Family;
    public fixed byte BDAddr[6];
    public byte Channel;
}