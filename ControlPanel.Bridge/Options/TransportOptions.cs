namespace ControlPanel.Bridge.Options;

public enum TransportType
{
    Serial,
    BtRfcomm
}

public class TransportOptions
{
    public required TransportType Type { get; init; }
    public required TimeSpan ReconnectInterval { get; init; }
}