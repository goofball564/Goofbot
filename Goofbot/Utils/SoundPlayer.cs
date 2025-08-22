namespace Goofbot.Utils;

using System.Threading.Tasks;
using CSCore.Codecs;
using CSCore;
using CSCore.SoundOut;
using System;
using System.IO;

internal class SoundPlayer
{
    private const float DefaultVolume = 0.15f;

    private readonly string soundFile;
    private readonly float volume;

    private IWaveSource waveSource;
    private WasapiOut soundOut;

    public SoundPlayer(string soundFile, float volume = DefaultVolume)
    {
        if (File.Exists(soundFile))
        {
            this.soundFile = soundFile;
            this.volume = volume;
            this.waveSource = CodecFactory.Instance.GetCodec(this.soundFile);
            this.soundOut = new();
            this.soundOut.Initialize(this.waveSource);
            this.soundOut.Volume = this.volume;
            this.soundOut.Stopped += this.OnStopped;
            this.soundOut.Play();
        }
    }

    private async void OnStopped(object sender, PlaybackStoppedEventArgs e)
    {
        await Task.Run(() =>
        {
            this.waveSource.Dispose();
            this.soundOut.Dispose();
        });
    }
}
