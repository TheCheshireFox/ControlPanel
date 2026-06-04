using System.IO.Ports;
using System.Text;
using ControlPanel.Bridge.Options;
using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge.Transport;

public sealed class SerialPortConnector(IOptions<TransportOptions> options) : IStreamConnector
{
    private readonly TransportOptions _options = options.Value;

    public Task<StreamConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        SerialPort? port = null;
        try
        {
            port = new SerialPort(_options.Tty, _options.BaudRate)
            {
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                Encoding = Encoding.UTF8,
                ReadTimeout = -1,
                WriteTimeout = -1,
                ReadBufferSize = 8192,
                WriteBufferSize = 8192,
                DtrEnable = _options.FlowControl,
                RtsEnable = _options.FlowControl
            };

            port.Open();

            return Task.FromResult(new StreamConnection(port.BaseStream, port));
        }
        catch
        {
            port?.Dispose();
            throw;
        }
    }
}
