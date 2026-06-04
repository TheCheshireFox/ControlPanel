namespace ControlPanel.Bridge.Options;

public class TransportOptions
{
    public required string Tty { get; init; }
    public required int BaudRate { get; init; } = 115200;
    public required TimeSpan ReconnectInterval { get; init; } = TimeSpan.FromSeconds(30);
}