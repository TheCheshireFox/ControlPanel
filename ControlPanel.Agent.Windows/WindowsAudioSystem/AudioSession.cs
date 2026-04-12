using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ControlPanel.Agent.Windows.WindowsAudioSystem;

public sealed class AudioSession : IDisposable
{
    private readonly AudioSessionControl _control;
    private readonly AudioSessionEventsHandler _audioSessionEventsHandler;

    private volatile bool _mute;
    private volatile float _volume;
    private volatile AudioSessionState _state;

    public string Id { get; }
    public string DisplayName { get; }
    public int ProcessId { get; }
    public bool IsSystemSoundsSession { get; }

    public bool Mute
    {
        get => _mute;
        set => _control.SimpleAudioVolume.Mute = _mute = value;
    }

    public float Volume
    {
        get => _volume;
        set => _control.SimpleAudioVolume.Volume = _volume = value;
    }
    
    public AudioSessionState State => _state;

    public event EventHandler<AudioSession>? OnSessionDisconnected;
    
    public AudioSession(IAudioSessionControl control)
    {
        _control = new AudioSessionControl(control);
        _audioSessionEventsHandler = new AudioSessionEventsHandler(this);
        _control.RegisterEventClient(_audioSessionEventsHandler);
        
        _mute = _control.SimpleAudioVolume.Mute;
        _volume = _control.SimpleAudioVolume.Volume;
        _state = _control.State;
        Id = _control.GetSessionInstanceIdentifier;
        DisplayName = _control.DisplayName;
        ProcessId = (int)_control.GetProcessID;
        IsSystemSoundsSession = _control.IsSystemSoundsSession;
    }
    
    private class AudioSessionEventsHandler(AudioSession session) : IAudioSessionEventsHandler
    {
        public void OnVolumeChanged(float volume, bool isMuted)
        {
            session._mute = isMuted;
            session._volume = volume;
        }

        public void OnStateChanged(AudioSessionState state)
        {
            session._state = state;
        }

        public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
        {
            try
            {
                session.OnSessionDisconnected?.Invoke(this, session);
            }
            catch
            {
                // NOP
            }
        }
        
        public void OnDisplayNameChanged(string displayName) { }
        public void OnIconPathChanged(string iconPath) { }
        public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex) { }
        public void OnGroupingParamChanged(ref Guid groupingId) { }
    }

    public void Dispose()
    {
        _control.UnRegisterEventClient(_audioSessionEventsHandler);
        _control.Dispose();
    }
}