using System.Collections.Concurrent;
using System.Threading.Channels;
using ControlPanel.Bridge.Agent;
using ControlPanel.Bridge.Audio;
using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Bridge.Device.Messaging;
using ControlPanel.Bridge.Framer;
using ControlPanel.Bridge.Options;
using ControlPanel.Protocol;
using ControlPanel.Shared.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DeviceAudioStreamId = ControlPanel.Bridge.Device.DeviceProtocol.AudioStreamId;

namespace ControlPanel.Bridge.UnitTests;

public class BridgeDeviceIntegrationTests
{
    private const string AgentId = "agent-a";

    [Test]
    public async Task RepositoryUpdate_SendsStreamsSnapshotToDevice()
    {
        await using var harness = BridgeHarness.Create();

        var repository = harness.Services.GetRequiredService<IAudioStreamRepository>();
        await repository.UpdateAsync(AgentId, [CreateAgentStream("stream-1")], TestContext.CurrentContext.CancellationToken);

        var message = await harness.Frames.ReceiveDeviceMessageAsync<StreamsDeviceMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(message.Updated, Has.Length.EqualTo(1));
            Assert.That(message.Deleted, Is.Empty);
            Assert.That(message.Updated[0].Id.Id, Is.EqualTo("stream-1"));
            Assert.That(message.Updated[0].Id.AgentId, Is.EqualTo(AgentId));
            Assert.That(message.Updated[0].Source, Is.EqualTo("firefox"));
            Assert.That(message.Updated[0].Name, Is.EqualTo("Firefox"));
            Assert.That(message.Updated[0].Mute, Is.False);
            Assert.That(message.Updated[0].Volume, Is.EqualTo(0.42));
            Assert.That(message.Updated[0].IconHash, Is.EqualTo(123));
        });
    }

    [Test]
    public async Task DeviceRequestRefresh_GetsCurrentRepositorySnapshot()
    {
        await using var harness = BridgeHarness.Create();
        await harness.StartDeviceMessagePumpAsync();

        var repository = harness.Services.GetRequiredService<IAudioStreamRepository>();
        await repository.UpdateAsync(AgentId, [CreateAgentStream("stream-1")], TestContext.CurrentContext.CancellationToken);

        await harness.Frames.SendFromDeviceAsync(new RequestRefreshDeviceMessage());
        var message = await harness.Frames.ReceiveDeviceMessageAsync<StreamsDeviceMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(message.Updated, Has.Length.EqualTo(1));
            Assert.That(message.Deleted, Is.Empty);
            Assert.That(message.Updated[0].Id.Id, Is.EqualTo("stream-1"));
            Assert.That(message.Updated[0].Id.AgentId, Is.EqualTo(AgentId));
        });
    }

    [Test]
    public async Task DeviceSetVolume_ForwardsToAgent()
    {
        await using var harness = BridgeHarness.Create();
        await harness.StartDeviceMessagePumpAsync();

        await harness.Frames.SendFromDeviceAsync(new SetVolumeDeviceMessage(new DeviceAudioStreamId("stream-1", AgentId), 0.5f));
        var message = await harness.AgentRegistry.ReadAsync<SetVolumeAgentMessage>(AgentId);

        Assert.Multiple(() =>
        {
            Assert.That(message.Id, Is.EqualTo("stream-1"));
            Assert.That(message.Volume, Is.EqualTo(0.5));
        });
    }

    [Test]
    public async Task DeviceSetMute_ForwardsToAgent()
    {
        await using var harness = BridgeHarness.Create();
        await harness.StartDeviceMessagePumpAsync();

        await harness.Frames.SendFromDeviceAsync(new SetMuteDeviceMessage(new DeviceAudioStreamId("stream-1", AgentId), true));
        var message = await harness.AgentRegistry.ReadAsync<SetMuteAgentMessage>(AgentId);

        Assert.Multiple(() =>
        {
            Assert.That(message.Id, Is.EqualTo("stream-1"));
            Assert.That(message.Mute, Is.True);
        });
    }

    [Test]
    public async Task DeviceGetIcon_CacheMiss_ForwardsRequestToAgent()
    {
        await using var harness = BridgeHarness.Create();
        await harness.StartDeviceMessagePumpAsync();

        await harness.Frames.SendFromDeviceAsync(new GetIconDeviceMessage("firefox", AgentId, 123));
        var message = await harness.AgentRegistry.ReadAsync<GetIconAgentMessage>(AgentId);

        Assert.That(message.Source, Is.EqualTo("firefox"));
    }

    [Test]
    public async Task DeviceGetIcon_CacheHit_ReturnsIconToDevice()
    {
        await using var harness = BridgeHarness.Create();
        await harness.StartDeviceMessagePumpAsync();
        var cache = harness.Services.GetRequiredService<IAudioStreamIconCache>();
        cache.AddIcon("firefox", AgentId, 123, new AudioCacheIcon(2, [0x01, 0x02, 0x03, 0x04]));

        await harness.Frames.SendFromDeviceAsync(new GetIconDeviceMessage("firefox", AgentId, 123));
        var message = await harness.Frames.ReceiveDeviceMessageAsync<IconDeviceMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(message.Source, Is.EqualTo("firefox"));
            Assert.That(message.AgentId, Is.EqualTo(AgentId));
            Assert.That(message.IconHash, Is.EqualTo(123));
            Assert.That(message.Size, Is.EqualTo(2));
            Assert.That(message.Icon, Is.EqualTo(new byte[] { 0x01, 0x02, 0x03, 0x04 }));
        });
    }

    private static AgentAudioStream CreateAgentStream(string id)
        => new(id, "firefox", "Firefox", false, 0.42, 123);

    private sealed class BridgeHarness : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private AsyncServiceScope? _messageScope;
        private Task? _messagePump;

        private BridgeHarness(ServiceProvider services, FakeFrameChannel frames, RecordingAgentRegistry agentRegistry)
        {
            Services = services;
            Frames = frames;
            AgentRegistry = agentRegistry;
        }

        public ServiceProvider Services { get; }
        public FakeFrameChannel Frames { get; }
        public RecordingAgentRegistry AgentRegistry { get; }

        public static BridgeHarness Create()
        {
            var services = new ServiceCollection();
            var frames = new FakeFrameChannel();
            var agents = new RecordingAgentRegistry();

            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new StreamsOptions()));
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new AudioStreamIconCacheOptions
            {
                CacheExpiry = TimeSpan.FromHours(1),
                CacheSizeKb = 128
            }));

            services.AddMediator(options =>
            {
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.Assemblies = [typeof(Program)];
            });

            services.AddSingleton<IFrameChannel>(frames);
            services.AddSingleton<DeviceMessageChannel>();
            services.AddSingleton<IDeviceConnection>(sp => sp.GetRequiredService<DeviceMessageChannel>());
            services.AddScoped<IMessageTransport<DeviceMessage>>(sp => sp.GetRequiredService<DeviceMessageChannel>());
            services.AddScoped<IMessageService<DeviceMessage>, MessageService<DeviceMessage>>();

            services.AddSingleton<IAudioStreamRepository, AudioStreamRepository>();
            services.AddSingleton<IAudioStreamIconCache, AudioStreamIconCache>();
            services.AddSingleton<IAgentRegistry>(agents);
            services.AddSingleton<AudioStreamIncrementalSnapshotHandler>();

            return new BridgeHarness(services.BuildServiceProvider(), frames, agents);
        }

        public Task StartDeviceMessagePumpAsync()
        {
            _messageScope = Services.CreateAsyncScope();
            var service = _messageScope.Value.ServiceProvider.GetRequiredService<IMessageService<DeviceMessage>>();
            _messagePump = service.RunAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();

            if (_messagePump != null)
                await _messagePump.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            if (_messageScope != null)
                await _messageScope.Value.DisposeAsync();

            await Services.DisposeAsync();
            _cts.Dispose();
        }
    }

    private sealed class FakeFrameChannel : IFrameChannel
    {
        private readonly Channel<byte[]> _fromDevice = Channel.CreateUnbounded<byte[]>();
        private readonly Channel<byte[]> _toDevice = Channel.CreateUnbounded<byte[]>();

        public Task WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
            => _toDevice.Writer.WriteAsync(payload.ToArray(), cancellationToken).AsTask();

        public IAsyncEnumerable<byte[]> ReadAsync(CancellationToken cancellationToken)
            => _fromDevice.Reader.ReadAllAsync(cancellationToken);

        public Task SendFromDeviceAsync(DeviceMessage message)
            => _fromDevice.Writer.WriteAsync(DeviceMessageSerializer.Serialize(message), TestContext.CurrentContext.CancellationToken).AsTask();

        public async Task<T> ReceiveDeviceMessageAsync<T>() where T : DeviceMessage
        {
            var bytes = await _toDevice.Reader.ReadAsync(TestContext.CurrentContext.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.CurrentContext.CancellationToken);
            return (T)DeviceMessageSerializer.Deserialize(bytes);
        }
    }

    private sealed class RecordingAgentRegistry : IAgentRegistry
    {
        private readonly ConcurrentDictionary<string, Channel<AgentMessage>> _messages = new();

        public Task AddAsync(IAgentConnection connection, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task RemoveAsync(IAgentConnection connection, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<bool> TrySendAsync(string agentId, AgentMessage message, CancellationToken cancellationToken)
        {
            var channel = _messages.GetOrAdd(agentId, _ => Channel.CreateUnbounded<AgentMessage>());
            channel.Writer.TryWrite(message);
            return Task.FromResult(true);
        }

        public async Task<T> ReadAsync<T>(string agentId) where T : AgentMessage
        {
            var channel = _messages.GetOrAdd(agentId, _ => Channel.CreateUnbounded<AgentMessage>());
            var message = await channel.Reader.ReadAsync(TestContext.CurrentContext.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.CurrentContext.CancellationToken);
            return (T)message;
        }
    }
}
