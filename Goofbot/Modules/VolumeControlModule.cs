using System;
using System.Diagnostics;
using System.Linq;
using CoreAudio;

namespace Goofbot.Modules
{
    internal class VolumeControlModule
    {
        private readonly AudioSessionVolumeControl darkSouls;
        private readonly AudioSessionVolumeControl spotify;

        public float SpotifyVolume
        {
            set
            {
                spotify.Volume = value;
            }
        }

        public float DarkSoulsVolume
        {
            set
            {
                darkSouls.Volume = value;
            }
        }
        public VolumeControlModule()
        {
            darkSouls = new AudioSessionVolumeControl(["DarkSoulsRemastered", "DARKSOULS"]);
            spotify = new AudioSessionVolumeControl("Spotify");
        }

        private class AudioSessionVolumeControl
        {
            private readonly string[] processNames;
            private AudioSessionControl2? audioSessionControl;

            public float Volume
            {
                set
                {
                    if (audioSessionControl == null || audioSessionControl.State == AudioSessionState.AudioSessionStateExpired)
                    {
                        RefreshSession();
                    }

                    if (value > 1.0f)
                        value = 1.0f;
                    else if (value < 0.0f)
                        value = 0.0f;
                    try
                    {
                        if (audioSessionControl != null)
                        {
                            audioSessionControl.SimpleAudioVolume.MasterVolume = value;
                        }
                    }
                    catch (Exception)
                    {
                        // Do nothing :)
                    }
                }
            }

            public AudioSessionVolumeControl(string processName)
            {
                this.processNames = [processName];
            }

            public AudioSessionVolumeControl(string[] processNames)
            {
                this.processNames = processNames;
            }

            public void RefreshSession()
            {
                MMDeviceEnumerator DevEnum = new MMDeviceEnumerator(Guid.NewGuid());
                MMDeviceCollection devices = DevEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (MMDevice device in devices)
                {
                    foreach (AudioSessionControl2 session in device.AudioSessionManager2.Sessions)
                    {
                        if (session.State == AudioSessionState.AudioSessionStateActive)
                        {
                            Process p = Process.GetProcessById((int)session.ProcessID);
                            if (processNames.Contains(p.ProcessName))
                            {
                                audioSessionControl = session;
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}
