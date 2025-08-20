using CSCore.Codecs;
using CSCore;
using CSCore.SoundOut;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Goofbot.Utils
{
    internal class SoundPlayer
    {
        private const float DefaultVolume = 0.15f;

        private readonly ISoundOut _soundOut;
        private readonly IWaveSource _waveSource;

        public SoundPlayer(string soundFile)
        {
            _waveSource = CodecFactory.Instance.GetCodec(soundFile);

            _soundOut = new WasapiOut();
            _soundOut.Initialize(_waveSource);
            _soundOut.Volume = DefaultVolume;
            _soundOut.Stopped += OnStopped;
        }

        public void Play()
        {
             _soundOut.Play();
        }

        public void OnStopped(object sender, PlaybackStoppedEventArgs e)
        {
            Task.Run(() => { _soundOut.Dispose(); _waveSource.Dispose(); });
        }
    }
}
