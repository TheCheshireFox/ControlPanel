namespace ControlPanel.Bridge.Options;

public class UartOptions
{
    public required string Tty { get; init; }
    public required int BaudRate { get; init; }
    public required TimeSpan ReconnectInterval { get; init; }
}