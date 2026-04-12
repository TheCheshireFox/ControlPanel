using System.Runtime.InteropServices;
using NAudio.CoreAudioApi.Interfaces;

namespace ControlPanel.Agent.Windows.Extensions;

public static class AudioSessionControlExtensions
{
    public static string GetSessionInstanceIdentifier(this IAudioSessionControl control)
    {
        Marshal.ThrowExceptionForHR(((IAudioSessionControl2)control).GetSessionInstanceIdentifier(out var sessionId));
        return sessionId;
    }
}