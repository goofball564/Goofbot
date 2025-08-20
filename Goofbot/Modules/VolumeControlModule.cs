namespace Goofbot.Modules;

using System;
using System.Diagnostics;
using System.Linq;
using CoreAudio;
internal class VolumeControlModule
{
    private readonly AudioSessionVolumeControl darkSouls;
    private readonly AudioSessionVolumeControl spotify;

    public VolumeControlModule()
    {
        this.darkSouls = new AudioSessionVolumeControl(["DarkSoulsRemastered", "DARKSOULS"]);
        this.spotify = new AudioSessionVolumeControl("Spotify");
    }

    public float SpotifyVolume
    {
        set
        {
            this.spotify.Volume = value;
        }
    }

    public float DarkSoulsVolume
    {
        set
        {
            this.darkSouls.Volume = value;
        }
    }

    private class AudioSessionVolumeControl
    {
        private readonly string[] processNames;
        private AudioSessionControl2? audioSessionControl;

        public AudioSessionVolumeControl(string processName)
        {
            this.processNames = [processName];
        }

        public AudioSessionVolumeControl(string[] processNames)
        {
            this.processNames = processNames;
        }

        public float Volume
        {
            set
            {
                if (this.audioSessionControl == null || this.audioSessionControl.State == AudioSessionState.AudioSessionStateExpired)
                {
                    this.RefreshSession();
                }

                if (value > 1.0f)
                {
                    value = 1.0f;
                }
                else if (value < 0.0f)
                {
                    value = 0.0f;
                }

                try
                {
                    if (this.audioSessionControl != null)
                    {
                        this.audioSessionControl.SimpleAudioVolume.MasterVolume = value;
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
}
