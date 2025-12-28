using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ControlPanel.BtRfcomm.Native;
using AddressFamily = ControlPanel.BtRfcomm.Native.AddressFamily;
using SocketOptionLevel = ControlPanel.BtRfcomm.Native.SocketOptionLevel;
using SocketType = ControlPanel.BtRfcomm.Native.SocketType;

namespace ControlPanel.BtRfcomm;

public static class BtRfcomm
{
    public static unsafe Stream Connect(string bdaddr, byte channel, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var fd = LibC.socket(AddressFamily.Bluetooth, SocketType.Stream, SocketProtocol.RfComm);
        if (fd < 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            SetBtSecurityLow(fd);
            
            var addr = new SockAddrRc
            {
                Family = AddressFamily.Bluetooth,
                Channel = channel
            };
            ParseAddress(bdaddr, new Span<byte>(addr.BDAddr, 6));

            var rc = LibC.connect(fd, ref addr, Marshal.SizeOf<SockAddrRc>());
            if (rc < 0)
            {
                var err = Marshal.GetLastWin32Error();
                if (err != (int)Errno.InProgress)
                    throw new Win32Exception(err);

                WaitForConnection(fd, ref timeout, cancellationToken);
            }
            
            var sock = new Socket(new SafeSocketHandle(fd, true))
            {
                ReceiveTimeout = 60000,
                SendTimeout = 60000
            };
            return new RfcommSocketStream(sock);
        }
        catch (Exception exc)
        {
            var rc = LibC.close(fd);
            if (rc != 0)
                throw new Win32Exception(Marshal.GetPInvokeErrorMessage(Marshal.GetLastWin32Error()), exc);
            
            throw;
        }
    }

    private static unsafe void WaitForConnection(int fd, ref TimeSpan timeout, CancellationToken cancellationToken)
    {
        var pfds = stackalloc PollFd[1]
        {
            new()
            {
                Fd = fd,
                Events = PollEvent.PollOut
            }
        };

        while (timeout > TimeSpan.Zero)
        {
            var sw = Stopwatch.StartNew();
            var pr = LibC.poll(pfds, 1, (int)Math.Min(timeout.TotalMilliseconds, 1000));
                    
            if (pr >= 0)
                break;
                    
            timeout -= sw.Elapsed;
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (timeout <= TimeSpan.Zero)
            throw new TimeoutException();

        uint len = sizeof(int);
        var rc = LibC.getsockopt(fd, SocketOptionLevel.Socket, (int)SocketOption.Error, out var soerr, ref len);
                
        if (rc != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
                
        if (soerr != 0)
            throw new Win32Exception(soerr);
    }

    private static void SetBtSecurityLow(int fd)
    {
        var sec = new BtSecurity { Level = BtSecurityLevel.Low, KeySize = 0 };
        if (LibC.setsockopt(fd, SocketOptionLevel.Bluetooth, (int)BtSocketOption.Security, ref sec, (uint)Marshal.SizeOf<BtSecurity>()) != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    
    private static void ParseAddress(string addr, Span<byte> dst)
    {
        var bytes = addr.Split(':').Reverse().Select(x => Convert.ToByte(x, 16)).ToArray();
        if (bytes.Length != 6)
            throw new ArgumentOutOfRangeException(nameof(addr));
        
        bytes.CopyTo(dst);
    }
}