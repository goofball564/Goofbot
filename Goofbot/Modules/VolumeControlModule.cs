using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoreAudio;
using SpotifyAPI.Web;

namespace Goofbot.Modules
{
    internal class VolumeControlModule
    {
        private AudioSessionVolumeControl darkSouls;
        private AudioSessionVolumeControl spotify;

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
            private string[] processNames;
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
