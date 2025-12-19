using ControlPanel.Protocol;

namespace ControlPanel.Bridge.UnitTests;

public class AudioStreamRepositoryTests
{
    private const string AgentId = "agent";
    private static readonly byte[] _iconPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x01, 0x03, 0x00, 0x00, 0x00, 0x25, 0xDB, 0x56, 0xCA, 0x00, 0x00, 0x00, 0x06, 0x50, 0x4C, 0x54, 0x45, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x55, 0xC2, 0xD3, 0x7E, 0x00, 0x00, 0x00, 0x02, 0x74, 0x52, 0x4E, 0x53, 0xFF, 0x00, 0xE5, 0xB7, 0x30, 0x4A, 0x00, 0x00, 0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0E, 0xC4, 0x00, 0x00, 0x0E, 0xC4, 0x01, 0x95, 0x2B,
        0x0E, 0x1B, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x08, 0x99, 0x63, 0x10, 0x03, 0x00, 0x00, 0x18, 0x00, 0x17, 0x7B, 0xE8, 0x55, 0xD4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];
    
    private static readonly Dictionary<int, BridgeAudioStream> _testStreams = new()
    {
        { 1, CreateStream(AgentId, 1, false, 0) },
        { 2, CreateStream(AgentId, 2, false, 0) },
        { 3, CreateStream(AgentId, 3, false, 0) },
    };

    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task UpdateAsync_FirstUpdate_OnlyChanged_ReturnsAll()
    {
        var repository = new AudioStreamRepository();

        await repository.UpdateAsync(_testStreams.Values.ToArray(), CancellationToken.None);
        var states = await repository.GetAsync(onlyChanged: true, CancellationToken.None);

        foreach (var state in states)
        {
            Assert.Multiple(() =>
            {
                Assert.That(_testStreams.TryGetValue(byte.Parse(state.Id), out var stream), Is.True);
                AssertStateEqual(state, stream!);
            });
        }
    }

    [Test]
    public async Task UpdateAsync_SecondCallUpdatesStream_OnlyChanged_ReturnsUpdatedStream()
    {
        var repository = new AudioStreamRepository();

        await repository.UpdateAsync(_testStreams.Values.ToArray(), CancellationToken.None);
        await repository.GetAsync(onlyChanged: true, CancellationToken.None);
        
        var updatedStream = CreateStream(AgentId, 1, true, 0);
        await repository.UpdateAsync([updatedStream], CancellationToken.None);
        
        var states = await repository.GetAsync(onlyChanged: true, CancellationToken.None);
        
        Assert.That(states, Has.Length.EqualTo(1));
        AssertStateEqual(states[0], updatedStream);
    }
    
    [Test]
    public async Task UpdateAsync_TwoEqualUpdates_OnlyChanged_ReturnsNothing()
    {
        var repository = new AudioStreamRepository();

        await repository.UpdateAsync(_testStreams.Values.ToArray(), CancellationToken.None);
        await repository.GetAsync(onlyChanged: true, CancellationToken.None);
        
        await repository.UpdateAsync(_testStreams.Values.ToArray(), CancellationToken.None);
        var states = await repository.GetAsync(onlyChanged: true, CancellationToken.None);
        
        Assert.That(states, Has.Length.EqualTo(0));
    }
    
    [Test]
    public async Task UpdateAsync_TwoEqualUpdates_NotOnlyChanged_ReturnsAll()
    {
        var repository = new AudioStreamRepository();

        await repository.UpdateAsync(_testStreams.Values.ToArray(), CancellationToken.None);
        await repository.GetAsync(onlyChanged: true, CancellationToken.None);
        
        await repository.UpdateAsync(_testStreams.Values.ToArray(), CancellationToken.None);
        var states = await repository.GetAsync(onlyChanged: false, CancellationToken.None);
        
        Assert.That(states, Has.Length.EqualTo(_testStreams.Count));
    }
    
    [Test]
    public async Task UpdateAsync_GetRgb565A8IconAsync_HasIconsForEachStream()
    {
        var repository = new AudioStreamRepository();

        await repository.UpdateAsync(_testStreams.Values.ToArray(), CancellationToken.None);
        var icons = await Task.WhenAll(_testStreams.Values.Select(async x => await repository.GetRgb565A8IconAsync(x.Id, x.AgentId, CancellationToken.None)));
        
        Assert.That(icons, Has.Length.EqualTo(_testStreams.Count));
    }

    private static void AssertStateEqual(AudioStreamState state, BridgeAudioStream stream)
    {
        Assert.Multiple(() =>
        {
            Assert.That(state.AgentId, Is.EqualTo(AgentId));
            Assert.That(state.Mute, Is.EqualTo(stream!.Mute));
            Assert.That(state.Volume, Is.EqualTo(stream.Volume));
        });
    }
    
    private static BridgeAudioStream CreateStream(string agentId, byte id, bool mute, double volume)
        => CreateStream(agentId, id, id.ToString(), mute, volume);
    
    private static BridgeAudioStream CreateStream(string agentId, byte id, string iconName, bool mute, double volume)
        => new(agentId, id.ToString(), new BridgeAudioStreamIcon(iconName, _iconPng), id.ToString(), mute, volume);
}