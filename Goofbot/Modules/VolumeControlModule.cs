using System;
using System.Diagnostics;
using System.Linq;
using CoreAudio;

namespace Goofbot.Modules
{
    internal class VolumeControlModule
    {
        private readonly AudioSessionVolumeControl _darkSouls;
        private readonly AudioSessionVolumeControl _spotify;

        public float SpotifyVolume
        {
            set
            {
                _spotify.Volume = value;
            }
        }

        public float DarkSoulsVolume
        {
            set
            {
                _darkSouls.Volume = value;
            }
        }
        public VolumeControlModule()
        {
            _darkSouls = new AudioSessionVolumeControl(["DarkSoulsRemastered", "DARKSOULS"]);
            _spotify = new AudioSessionVolumeControl("Spotify");
        }

        private class AudioSessionVolumeControl
        {
            private readonly string[] _processNames;
            private AudioSessionControl2? _audioSessionControl;

            public float Volume
            {
                set
                {
                    if (_audioSessionControl == null || _audioSessionControl.State == AudioSessionState.AudioSessionStateExpired)
                    {
                        RefreshSession();
                    }

                    if (value > 1.0f)
                        value = 1.0f;
                    else if (value < 0.0f)
                        value = 0.0f;
                    try
                    {
                        if (_audioSessionControl != null)
                        {
                            _audioSessionControl.SimpleAudioVolume.MasterVolume = value;
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
                this._processNames = [processName];
            }

            public AudioSessionVolumeControl(string[] processNames)
            {
                this._processNames = processNames;
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
                            if (_processNames.Contains(p.ProcessName))
                            {
                                _audioSessionControl = session;
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}
