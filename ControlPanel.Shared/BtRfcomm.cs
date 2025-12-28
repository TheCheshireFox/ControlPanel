using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ControlPanel.Shared;

[StructLayout(LayoutKind.Sequential)]
struct bt_security
{
    public byte level;
    public byte key_size;
}

[StructLayout(LayoutKind.Sequential)]
file struct SockAddrRc
{
    public ushort rc_family;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public byte[] rc_bdaddr;

    public byte rc_channel;
}

[StructLayout(LayoutKind.Sequential)]
file struct pollfd
{
    public int fd;
    public short events;
    public short revents;
}

file static unsafe class Native
{
    public const int AF_BLUETOOTH = 31;
    public const int SOCK_STREAM = 1;
    public const int BTPROTO_RFCOMM = 3;
    
    public const int SOL_SOCKET = 1;
    public const int SOL_BLUETOOTH = 274;
    public const int SO_ERROR = 4;
    public const short POLLOUT = 0x004;
    
    public const int BT_SECURITY   = 4;
    public const byte BT_SECURITY_LOW = 1;
    
    public const int EINPROGRESS = 115;

    [DllImport("libc", SetLastError = true)]
    public static extern int socket(int domain, int type, int protocol);

    [DllImport("libc", SetLastError = true)]
    public static extern int connect(int sockfd, ref SockAddrRc addr, int addrlen);

    [DllImport("libc", SetLastError = true)]
    public static extern int close(int fd);
    
    [DllImport("libc", SetLastError = true)]
    public static extern int poll(pollfd* fds, uint nfds, int timeout);
    
    [DllImport("libc", SetLastError = true)]
    public static extern int getsockopt(int fd, int level, int optname, out int optval, ref uint optlen);
    
    [DllImport("libc", SetLastError = true)]
    public static extern int setsockopt(int sockfd, int level, int optname, ref bt_security optval, uint optlen);

}

public sealed class RfcommSocketStream : Stream
{
    private readonly Socket _socket;

    public RfcommSocketStream(Socket socket)
    {
        _socket = socket;
    }
    
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    
    public override void Flush() { }
    
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    
    public override void SetLength(long value) => throw new NotSupportedException();
    
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => await _socket.ReceiveAsync(buffer, cancellationToken);
    
    public override int Read(byte[] buffer, int offset, int count) => Read(new Span<byte>(buffer, offset, count));
    public override int Read(Span<byte> buffer) => _socket.Receive(buffer);
    
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => await _socket.SendAsync(buffer, cancellationToken);
    
    public override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<byte>(buffer, offset, count));
    
    public override void Write(ReadOnlySpan<byte> buffer) => _socket.Send(buffer);
    
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _socket.Dispose();
    }
}

public static class BtRfcomm
{
    public static unsafe Stream Connect(string bdaddr, byte channel, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var fd = Native.socket(Native.AF_BLUETOOTH, Native.SOCK_STREAM, Native.BTPROTO_RFCOMM);
        if (fd < 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            SetBtSecurityLow(fd);
            
            var addr = new SockAddrRc
            {
                rc_family = Native.AF_BLUETOOTH,
                rc_bdaddr = ParseAddress(bdaddr),
                rc_channel = channel
            };

            var rc = Native.connect(fd, ref addr, Marshal.SizeOf<SockAddrRc>());
            if (rc < 0)
            {
                var err = Marshal.GetLastWin32Error();
                if (err != Native.EINPROGRESS)
                    throw new Win32Exception(err);

                var pfds = stackalloc pollfd[1]
                {
                    new()
                    {
                        fd = fd,
                        events = Native.POLLOUT
                    }
                };

                while (timeout > TimeSpan.Zero)
                {
                    var sw = Stopwatch.StartNew();
                    var pr = Native.poll(pfds, 1, (int)Math.Min(timeout.TotalMilliseconds, 1000));
                    
                    if (pr >= 0)
                        break;
                    
                    timeout -= sw.Elapsed;
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (timeout <= TimeSpan.Zero)
                    throw new TimeoutException();

                uint len = sizeof(int);
                rc = Native.getsockopt(fd, Native.SOL_SOCKET, Native.SO_ERROR, out var soerr, ref len);
                
                if (rc != 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                
                if (soerr != 0)
                    throw new Win32Exception(soerr);
            }

            var ssh = new SafeSocketHandle(fd, true);
            var sock = new Socket(ssh)
            {
                ReceiveTimeout = 60000,
                SendTimeout = 60000
            };
            return new RfcommSocketStream(sock);
        }
        catch (Exception)
        {
            _ = Native.close(fd);
            throw;
        }
    }

    private static void SetBtSecurityLow(int fd)
    {
        var sec = new bt_security { level = Native.BT_SECURITY_LOW, key_size = 0 };
        if (Native.setsockopt(fd, Native.SOL_BLUETOOTH, Native.BT_SECURITY, ref sec, (uint)Marshal.SizeOf<bt_security>()) != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    
    private static byte[] ParseAddress(string addr)
    {
        return addr.Split(':').Reverse().Select(x => Convert.ToByte(x, 16)).ToArray();
    }
}