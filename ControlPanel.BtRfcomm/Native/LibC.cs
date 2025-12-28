using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ControlPanel.BtRfcomm.Native;

internal enum AddressFamily : ushort
{
    Bluetooth = 31
}

internal enum SocketType
{
    Stream = 1
}

internal enum SocketProtocol
{
    RfComm = 3
}

internal enum SocketOptionLevel
{
    Socket = 1,
    Bluetooth = 274
}

internal enum SocketOption
{
    Error = 4
}

internal enum BtSocketOption
{
    Security = 4
}

internal enum PollEvent : short
{
    PollOut = 0x004
}

internal enum Errno
{
    InProgress = 115
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static unsafe partial class LibC
{
    [LibraryImport("libc", SetLastError = true)]
    public static partial int socket(AddressFamily domain, SocketType type, SocketProtocol protocol);

    [LibraryImport("libc", SetLastError = true)]
    public static partial int connect(int sockfd, ref SockAddrRc addr, int addrlen);

    [LibraryImport("libc", SetLastError = true)]
    public static partial int close(int fd);
    
    [LibraryImport("libc", SetLastError = true)]
    public static partial int poll(PollFd* fds, uint nfds, int timeout);
    
    [LibraryImport("libc", SetLastError = true)]
    public static partial int getsockopt(int fd, SocketOptionLevel level, int optname, out int optval, ref uint optlen);
    
    [LibraryImport("libc", SetLastError = true)]
    public static partial int setsockopt(int sockfd, SocketOptionLevel level, int optname, ref BtSecurity optval, uint optlen);
}