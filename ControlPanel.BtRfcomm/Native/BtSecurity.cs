using System.Runtime.InteropServices;

namespace ControlPanel.BtRfcomm.Native;

internal enum BtSecurityLevel : byte
{
    Low = 1
}

[StructLayout(LayoutKind.Sequential)]
internal struct BtSecurity
{
    public BtSecurityLevel Level;
    public byte KeySize;
}