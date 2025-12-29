using ControlPanel.Bridge.Extensions;
using ControlPanel.Bridge.Protocol;

namespace ControlPanel.Bridge;

public class ControlPanelBridge : BackgroundService
{
    private readonly IControllerConnection _controllerConnection;
    private readonly IAudioStreamRepository _audioStreamRepository;
    private readonly IBridgeCommandHandler _commandHandler;
    private readonly ILogger<ControlPanelBridge> _logger;
    private readonly ITextRenderer _textRenderer;

    public ControlPanelBridge(IControllerConnection controllerConnection, IAudioStreamRepository audioStreamRepository, IBridgeCommandHandler commandHandler, ITextRenderer textRenderer, ILogger<ControlPanelBridge> logger)
    {
        _controllerConnection = controllerConnection;
        _audioStreamRepository = audioStreamRepository;
        _commandHandler = commandHandler;
        _textRenderer = textRenderer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _audioStreamRepository.OnSnapshotChangedAsync += OnStreamsUpdateAsync;
        try
        {
            await foreach (var message in _controllerConnection.ReadMessagesAsync(stoppingToken))
            {
                try
                {
                    await _commandHandler.HandleAsync(message, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing commands");
                }
            }
        }
        finally
        {
            _audioStreamRepository.OnSnapshotChangedAsync -= OnStreamsUpdateAsync;
        }
    }

    private async Task OnStreamsUpdateAsync(AudioStreamIncrementalSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.Deleted.Length == 0 && snapshot.Updated.Length == 0)
            return;
        
        var (updated, deleted) = snapshot.ToUartAudioStreams(_textRenderer);
        var msg = new UartStreamsMessage(updated, deleted);
        
        _logger.LogDebug("Sending streams, updated: {Updated}, deleted: {Deleted}", updated.Length, deleted.Length);

        try
        {
            await _controllerConnection.SendMessageAsync(msg, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending streams");
        }
    }
}