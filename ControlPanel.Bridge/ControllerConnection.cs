using System.Runtime.CompilerServices;
using ControlPanel.Bridge.Framer;
using ControlPanel.Bridge.Protocol;

namespace ControlPanel.Bridge;

public interface IControllerConnection
{
    IAsyncEnumerable<UartMessage> ReadMessagesAsync(CancellationToken cancellationToken);
    Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : UartMessage;
}

public class ControllerConnection : IControllerConnection
{
    private static readonly UartMessageSerializer _serializer = new();

    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private readonly int _retryCount = 3;
    
    private readonly IFrameProtocol _protocol;
    private readonly ILogger<ControllerConnection> _logger;

    public ControllerConnection(IFrameProtocol protocol, ILogger<ControllerConnection> logger)
    {
        _protocol = protocol;
        _logger = logger;
    }

    public async IAsyncEnumerable<UartMessage> ReadMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var data in _protocol.ReadAsync(cancellationToken))
        {
            UartMessage? message = null;
            try
            {
                message = _serializer.Deserialize(data);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing commands");
            }
            
            if (message != null)
                yield return message;
        }
    }

    public async Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : UartMessage
    {
        await _protocol.SendAsync(_serializer.Serialize(message), _timeout, _retryCount, cancellationToken);
    }
}