using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Bridge.Device.Messaging;

namespace ControlPanel.Bridge.UnitTests;

public class DeviceMessageWireContractTests
{
    [Test]
    public void MessageTypeValues_MatchDeviceFirmwareEnum()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)MessageType.Streams, Is.EqualTo(0));
            Assert.That((int)MessageType.SetVolume, Is.EqualTo(1));
            Assert.That((int)MessageType.SetMute, Is.EqualTo(2));
            Assert.That((int)MessageType.Icon, Is.EqualTo(3));
            Assert.That((int)MessageType.GetIcon, Is.EqualTo(4));
            Assert.That((int)MessageType.RequestRefresh, Is.EqualTo(5));
        });
    }

    [Test]
    public void Bridge_AcceptsDeviceAuthoredSetVolumeMessageFixture()
    {
        var message = DeviceMessageSerializer.Deserialize(ReadFixture("device/set-volume.msgpack"));

        Assert.That(message, Is.TypeOf<SetVolumeDeviceMessage>());
        var setVolume = (SetVolumeDeviceMessage)message;
        Assert.Multiple(() =>
        {
            Assert.That(setVolume.Type, Is.EqualTo(MessageType.SetVolume));
            Assert.That(setVolume.Id.Id, Is.EqualTo("stream-1"));
            Assert.That(setVolume.Id.AgentId, Is.EqualTo("agent-a"));
            Assert.That(setVolume.Volume, Is.EqualTo(0.42));
        });
    }

    [Test]
    public void Bridge_AcceptsDeviceAuthoredSetMuteMessageFixture()
    {
        var message = DeviceMessageSerializer.Deserialize(ReadFixture("device/set-mute.msgpack"));

        Assert.That(message, Is.TypeOf<SetMuteDeviceMessage>());
        var setMute = (SetMuteDeviceMessage)message;
        Assert.Multiple(() =>
        {
            Assert.That(setMute.Type, Is.EqualTo(MessageType.SetMute));
            Assert.That(setMute.Id.Id, Is.EqualTo("stream-1"));
            Assert.That(setMute.Id.AgentId, Is.EqualTo("agent-a"));
            Assert.That(setMute.Mute, Is.True);
        });
    }

    [Test]
    public void Bridge_AcceptsDeviceAuthoredGetIconMessageFixture()
    {
        var message = DeviceMessageSerializer.Deserialize(ReadFixture("device/get-icon.msgpack"));

        Assert.That(message, Is.TypeOf<GetIconDeviceMessage>());
        var getIcon = (GetIconDeviceMessage)message;
        Assert.Multiple(() =>
        {
            Assert.That(getIcon.Type, Is.EqualTo(MessageType.GetIcon));
            Assert.That(getIcon.Source, Is.EqualTo("firefox"));
            Assert.That(getIcon.AgentId, Is.EqualTo("agent-a"));
            Assert.That(getIcon.IconHash, Is.EqualTo(123));
        });
    }

    [Test]
    public void Bridge_AcceptsDeviceAuthoredRequestRefreshMessageFixture()
    {
        var message = DeviceMessageSerializer.Deserialize(ReadFixture("device/request-refresh.msgpack"));

        Assert.That(message, Is.TypeOf<RequestRefreshDeviceMessage>());
        Assert.That(message.Type, Is.EqualTo(MessageType.RequestRefresh));
    }

    [Test]
    public void BridgeStreamsMessage_MatchesWireFixture()
    {
        var message = new StreamsDeviceMessage(
            [
                new AudioStream(
                    new Device.DeviceProtocol.AudioStreamId("stream-1", "agent-a"),
                    "firefox",
                    "Firefox",
                    false,
                    0.42,
                    123)
            ],
            [new Device.DeviceProtocol.AudioStreamId("stream-2", "agent-a")]);

        Assert.That(DeviceMessageSerializer.Serialize(message), Is.EqualTo(ReadFixture("bridge/streams.msgpack")));
    }

    [Test]
    public void BridgeIconMessage_MatchesWireFixture()
    {
        var message = new IconDeviceMessage("firefox", "agent-a", 123, 2, [0x01, 0x02, 0x03, 0x04]);

        Assert.That(DeviceMessageSerializer.Serialize(message), Is.EqualTo(ReadFixture("bridge/icon.msgpack")));
    }

    private static byte[] ReadFixture(string relativePath)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            var path = Path.Combine(directory.FullName, "ControlPanel.ProtocolFixtures", relativePath);
            if (File.Exists(path))
                return File.ReadAllBytes(path);

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Unable to locate protocol fixture '{relativePath}'.");
    }
}
