using System.Runtime.CompilerServices;
using ControlPanel.Bridge.Framer;
using ControlPanel.Bridge.Protocol;

namespace ControlPanel.Bridge;

public interface IControllerConnection
{
    IAsyncEnumerable<Message> ReadMessagesAsync(CancellationToken cancellationToken);
    Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : Message;
}

public class ControllerConnection : IControllerConnection
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private readonly int _retryCount = 3;
    
    private readonly IFrameProtocol _protocol;
    private readonly ILogger<ControllerConnection> _logger;

    public ControllerConnection(IFrameProtocol protocol, ILogger<ControllerConnection> logger)
    {
        _protocol = protocol;
        _logger = logger;
    }

    public async IAsyncEnumerable<Message> ReadMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var data in _protocol.ReadAsync(cancellationToken))
        {
            Message? message = null;
            try
            {
                message = MessageSerializer.Deserialize(data);
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

    public async Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : Message
    {
        await _protocol.SendAsync(MessageSerializer.Serialize(message), _timeout, _retryCount, cancellationToken);
    }
}