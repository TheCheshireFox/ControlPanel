using System.Buffers;
using Microsoft.Extensions.Logging.Abstractions;
using BridgeFramer = ControlPanel.Bridge.Framer.Framer;

namespace ControlPanel.Bridge.UnitTests;

public class FramerTests
{
    private static readonly byte[] Magic = [0xAB, 0xBC];

    [Test]
    public void ToBytes_WritesMagicBigEndianLengthAndPayload()
    {
        var framer = CreateFramer();

        var frame = framer.ToBytes(new byte[] { 0x01, 0x02, 0x03 });

        Assert.That(frame, Is.EqualTo(new byte[] { 0xAB, 0xBC, 0x00, 0x03, 0x01, 0x02, 0x03 }));
    }

    [Test]
    public void TryParseFrame_SkipsGarbageAndParsesConsecutiveFrames()
    {
        var framer = CreateFramer();
        var bytes = new byte[]
        {
            0x00, 0xFF,
            0xAB, 0xBC, 0x00, 0x03, 0x01, 0x02, 0x03,
            0xAB, 0xBC, 0x00, 0x01, 0x04
        };
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));

        var parsedFirst = framer.TryParseFrame(ref reader, out var firstFrame);
        var parsedSecond = framer.TryParseFrame(ref reader, out var secondFrame);
        var readerEnded = reader.End;

        Assert.Multiple(() =>
        {
            Assert.That(parsedFirst, Is.True);
            Assert.That(firstFrame!.Value.ToArray(), Is.EqualTo(new byte[] { 0x01, 0x02, 0x03 }));
            Assert.That(parsedSecond, Is.True);
            Assert.That(secondFrame!.Value.ToArray(), Is.EqualTo(new byte[] { 0x04 }));
            Assert.That(readerEnded, Is.True);
        });
    }

    [Test]
    public void TryParseFrame_IncompleteFrame_DoesNotConsumeCandidateFrame()
    {
        var framer = CreateFramer();
        var bytes = new byte[] { 0xAB, 0xBC, 0x00, 0x03, 0x01 };
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));

        var parsed = framer.TryParseFrame(ref reader, out var frame);
        var consumed = reader.Consumed;

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.False);
            Assert.That(frame, Is.Null);
            Assert.That(consumed, Is.Zero);
        });
    }

    private static BridgeFramer CreateFramer()
        => new(Magic, NullLogger.Instance);
}
