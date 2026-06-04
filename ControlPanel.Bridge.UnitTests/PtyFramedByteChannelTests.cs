using System.Runtime.InteropServices;
using ControlPanel.Bridge.Framer;
using ControlPanel.Bridge.Options;
using ControlPanel.Bridge.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace ControlPanel.Bridge.UnitTests;

[Platform(Include = "Linux")]
public class PtyFramedByteChannelTests
{
    [Test]
    public async Task WriteAsync_SendsFramedPayloadToPseudoTerminal()
    {
        await using var pty = PseudoTerminal.Open();
        await using var channel = CreateChannel(pty.SlaveName);
        var payload = new byte[] { 0x01, 0x02, 0x03 };

        await channel.WriteAsync(payload, TestContext.CurrentContext.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.CurrentContext.CancellationToken);
        var frame = await pty.ReadMasterExactlyAsync(4 + payload.Length);

        Assert.That(frame, Is.EqualTo(new byte[] { 0xAB, 0xBC, 0x00, 0x03, 0x01, 0x02, 0x03 }));
    }

    [Test]
    public async Task ReadAsync_ParsesFramedPayloadFromPseudoTerminal()
    {
        await using var pty = PseudoTerminal.Open();
        await using var channel = CreateChannel(pty.SlaveName);
        var frame = new byte[] { 0xAB, 0xBC, 0x00, 0x03, 0x01, 0x02, 0x03 };

        var readTask = ReadOneFrameWithTimeoutAsync(channel);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.CurrentContext.CancellationToken);
        await pty.WriteMasterAsync(frame);
        var payload = await readTask;

        Assert.That(payload, Is.EqualTo(new byte[] { 0x01, 0x02, 0x03 }));
    }

    private static FramedByteChannel CreateChannel(string tty)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new TransportOptions
        {
            Tty = tty,
            BaudRate = 115200,
            ReconnectInterval = TimeSpan.FromMilliseconds(10),
            FlowControl = false
        });
        var connector = new SerialPortConnector(options);

        return new FramedByteChannel(connector, options, NullLogger<FramedByteChannel>.Instance);
    }

    private static async Task<byte[]> ReadOneFrameWithTimeoutAsync(IFrameChannel channel)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CurrentContext.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        await foreach (var frame in channel.ReadAsync(cts.Token))
            return frame;

        throw new TimeoutException("Timed out waiting for frame from pseudo-terminal.");
    }

    private sealed class PseudoTerminal : IAsyncDisposable
    {
        private const int EAgain = 11;
        private const int FGetFl = 3;
        private const int FSetFl = 4;
        private const int ONonBlock = 0x0800;

        private readonly int _masterFd;
        private readonly SafeFileHandle _masterHandle;

        private PseudoTerminal(int masterFd, SafeFileHandle masterHandle, string slaveName)
        {
            _masterFd = masterFd;
            _masterHandle = masterHandle;
            SlaveName = slaveName;
        }

        public string SlaveName { get; }

        public static PseudoTerminal Open()
        {
            const int oRdWr = 0x0002;
            const int oNoCtty = 0x0100;
            const int oCloExec = 0x80000;

            var fd = posix_openpt(oRdWr | oNoCtty | oCloExec);
            if (fd < 0)
                ThrowLibcError(nameof(posix_openpt));

            var handle = new SafeFileHandle((IntPtr)fd, ownsHandle: true);
            try
            {
                if (grantpt(fd) != 0)
                    ThrowLibcError(nameof(grantpt));

                if (unlockpt(fd) != 0)
                    ThrowLibcError(nameof(unlockpt));

                var flags = fcntl(fd, FGetFl, 0);
                if (flags < 0 || fcntl(fd, FSetFl, flags | ONonBlock) != 0)
                    ThrowLibcError(nameof(fcntl));

                var slaveNamePtr = ptsname(fd);
                if (slaveNamePtr == IntPtr.Zero)
                    ThrowLibcError(nameof(ptsname));

                var slaveName = Marshal.PtrToStringAnsi(slaveNamePtr)
                    ?? throw new InvalidOperationException("Unable to get pseudo-terminal slave name.");
                var result = new PseudoTerminal(fd, handle, slaveName);
                handle = null!;

                return result;
            }
            finally
            {
                handle?.Dispose();
            }
        }

        public async Task<byte[]> ReadMasterExactlyAsync(int size)
        {
            var buffer = new byte[size];
            var offset = 0;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);

            while (offset < size)
            {
                TestContext.CurrentContext.CancellationToken.ThrowIfCancellationRequested();

                var chunk = new byte[size - offset];
                var readCount = read(_masterFd, chunk, (nuint)chunk.Length);
                if (readCount > 0)
                {
                    Array.Copy(chunk, 0, buffer, offset, readCount);
                    offset += readCount;
                    continue;
                }

                if (readCount < 0 && Marshal.GetLastPInvokeError() != EAgain)
                    ThrowLibcError(nameof(read));

                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Timed out reading from pseudo-terminal master.");

                await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.CurrentContext.CancellationToken);
            }

            return buffer;
        }

        public async Task WriteMasterAsync(ReadOnlyMemory<byte> data)
        {
            var offset = 0;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);

            while (offset < data.Length)
            {
                TestContext.CurrentContext.CancellationToken.ThrowIfCancellationRequested();

                var chunk = data[offset..].ToArray();
                var written = write(_masterFd, chunk, (nuint)chunk.Length);
                if (written > 0)
                {
                    offset += written;
                    continue;
                }

                if (written < 0 && Marshal.GetLastPInvokeError() != EAgain)
                    ThrowLibcError(nameof(write));

                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Timed out writing to pseudo-terminal master.");

                await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.CurrentContext.CancellationToken);
            }
        }

        public ValueTask DisposeAsync()
        {
            _masterHandle.Dispose();
            return ValueTask.CompletedTask;
        }

        private static void ThrowLibcError(string function)
            => throw new InvalidOperationException($"{function} failed: {Marshal.GetLastPInvokeError()}.");

        [DllImport("libc", SetLastError = true)]
        private static extern int posix_openpt(int flags);

        [DllImport("libc", SetLastError = true)]
        private static extern int grantpt(int fd);

        [DllImport("libc", SetLastError = true)]
        private static extern int unlockpt(int fd);

        [DllImport("libc", SetLastError = true)]
        private static extern IntPtr ptsname(int fd);

        [DllImport("libc", SetLastError = true)]
        private static extern int fcntl(int fd, int cmd, int arg);

        [DllImport("libc", SetLastError = true)]
        private static extern int read(int fd, byte[] buffer, nuint count);

        [DllImport("libc", SetLastError = true)]
        private static extern int write(int fd, byte[] buffer, nuint count);
    }
}
