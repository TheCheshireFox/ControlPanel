using ControlPanel.Protocol;

namespace ControlPanel.Bridge;

public interface IControlPanelConnection
{
    Task SendStreamsAsync(BridgeAudioStream[] streams);
}