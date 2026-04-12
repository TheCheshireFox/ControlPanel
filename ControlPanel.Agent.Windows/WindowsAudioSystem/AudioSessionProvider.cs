using System.Collections.Concurrent;
using ControlPanel.Agent.Windows.Extensions;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ControlPanel.Agent.Windows.WindowsAudioSystem;

public sealed class AudioSessionProvider : IDisposable, IAudioSessionProvider
{
    private readonly MMDeviceEnumerator _deviceEnumerator = new();
    private readonly MMNotificationClient _notificationClient;
    private readonly ConcurrentDictionary<string, MMDevice> _devices = [];
    private readonly ConcurrentDictionary<string, AudioSession> _sessions = [];

    public IEnumerable<AudioSession> Sessions => _sessions.Values;
    
    public AudioSessionProvider()
    {
        _notificationClient = new MMNotificationClient(this);
        _deviceEnumerator.RegisterEndpointNotificationCallback(_notificationClient);
    }

    private void OnDeviceAdded(string pwstrDeviceId)
    {
        _devices.GetOrAdd(pwstrDeviceId, id =>
        {
            var device = _deviceEnumerator.GetDevice(id);
            device.AudioSessionManager.OnSessionCreated += OnSessionCreated;
            return device;
        });
    }

    private void OnSessionCreated(object sender, IAudioSessionControl newSession)
    {
        _sessions.GetOrAdd(newSession.GetSessionInstanceIdentifier(), _ =>
        {
            var session = new AudioSession(newSession);
            session.OnSessionDisconnected += OnSessionDisconnected;
            return session;
        });
    }

    private void OnSessionDisconnected(object? sender, AudioSession session)
    {
        Task.Run(() =>
        {
            if (_sessions.TryRemove(session.Id, out var audioSession))
                audioSession.Dispose();
        });
    }

    private void OnDeviceRemoved(string deviceId)
    {
        _devices.TryRemove(deviceId, out _);
    }
    
    // ReSharper disable once InconsistentNaming
    private class MMNotificationClient(AudioSessionProvider provider) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnDeviceAdded(string pwstrDeviceId) => provider.OnDeviceAdded(pwstrDeviceId);
        public void OnDeviceRemoved(string deviceId) => provider.OnDeviceRemoved(deviceId);
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }

    public void Dispose()
    {
        _deviceEnumerator.UnregisterEndpointNotificationCallback(_notificationClient);
        
        foreach (var device in _devices.Values)
            device.Dispose();
        
        foreach (var session in _sessions.Values)
            session.Dispose();

        _deviceEnumerator.Dispose();
    }
}