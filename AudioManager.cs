using NAudio.CoreAudioApi;

namespace WindowsMixer;

internal sealed class AudioManager : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public List<AppAudioSession> GetSessionsForProcess(int processId)
    {
        var results = new List<AppAudioSession>();
        try
        {
            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in devices)
            {
                try
                {
                    var sessions = device.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var ctl = sessions[i];
                        if ((int)ctl.GetProcessID == processId)
                            results.Add(new AppAudioSession(ctl));
                    }
                }
                catch { }
            }
        }
        catch { }
        return results;
    }

    public List<AppAudioSession> GetSessionsForProcessName(string processName)
    {
        var pids = new HashSet<int>();
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName(processName))
                pids.Add(p.Id);
        }
        catch { }

        if (pids.Count == 0) return new List<AppAudioSession>();

        var results = new List<AppAudioSession>();
        try
        {
            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in devices)
            {
                try
                {
                    var sessions = device.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var ctl = sessions[i];
                        if (pids.Contains((int)ctl.GetProcessID))
                            results.Add(new AppAudioSession(ctl));
                    }
                }
                catch { }
            }
        }
        catch { }
        return results;
    }

    public void Dispose() => _enumerator.Dispose();
}

internal sealed class AppAudioSession
{
    private readonly AudioSessionControl _ctl;

    public AppAudioSession(AudioSessionControl ctl) => _ctl = ctl;

    public int ProcessId => (int)_ctl.GetProcessID;

    public float Volume
    {
        get { try { return _ctl.SimpleAudioVolume.Volume; } catch { return 1f; } }
        set { try { _ctl.SimpleAudioVolume.Volume = Math.Clamp(value, 0f, 1f); } catch { } }
    }

    public bool IsMuted
    {
        get { try { return _ctl.SimpleAudioVolume.Mute; } catch { return false; } }
        set { try { _ctl.SimpleAudioVolume.Mute = value; } catch { } }
    }

    public void ToggleMute() => IsMuted = !IsMuted;
}
