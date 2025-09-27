namespace Goofbot.UtilClasses;

using System;
using System.Diagnostics;
using System.Linq;
using CoreAudio;
internal class AudioSessionControl
{
    private readonly string[] processNames;
    private AudioSessionControl2 audioSessionControl;

    public AudioSessionControl(string processName)
    {
        this.processNames = [processName];
    }

    public AudioSessionControl(string[] processNames)
    {
        this.processNames = processNames;
    }

    public int Volume
    {
        set
        {
            if (this.audioSessionControl == null || this.audioSessionControl.State == AudioSessionState.AudioSessionStateExpired)
            {
                this.RefreshSession();
            }

            if (value > 100)
            {
                value = 100;
            }
            else if (value < 0)
            {
                value = 0;
            }

            try
            {
                if (this.audioSessionControl != null)
                {
                    this.audioSessionControl.SimpleAudioVolume.MasterVolume = (float)(value / 100.0);
                }
            }
            catch (Exception)
            {
                // Do nothing :)
            }
        }
    }

    public void RefreshSession()
    {
        MMDeviceEnumerator devEnum = new (Guid.NewGuid());
        MMDeviceCollection devices = devEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (MMDevice device in devices)
        {
            foreach (AudioSessionControl2 session in device.AudioSessionManager2.Sessions)
            {
                if (session.State == AudioSessionState.AudioSessionStateActive)
                {
                    Process p = Process.GetProcessById((int)session.ProcessID);
                    if (this.processNames.Contains(p.ProcessName))
                    {
                        this.audioSessionControl = session;
                        return;
                    }
                }
            }
        }
    }
}
