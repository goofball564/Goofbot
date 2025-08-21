namespace Goofbot.Utils;

using System.Threading.Tasks;
using CSCore.Codecs;
using CSCore;
using CSCore.SoundOut;
using System;

internal class SoundPlayer
{
    private const float DefaultVolume = 0.15f;

    private readonly string soundFile;
    private readonly float volume;

    private IWaveSource waveSource;
    private WasapiOut soundOut;

    private int remainingTimesToLoop;

    public SoundPlayer(string soundFile, float volume = DefaultVolume, int numberTimesToLoop = 1)
    {
        this.soundFile = soundFile;
        this.volume = volume;
        this.remainingTimesToLoop = numberTimesToLoop;

        this.CreateSoundOut();
    }

    private async void OnStopped(object sender, PlaybackStoppedEventArgs e)
    {
        await this.Dispose();
        if (this.remainingTimesToLoop > 0)
        {
            this.CreateSoundOut();
        }
    }

    private async Task Dispose()
    {
        await Task.Run(() =>
        {
            this.waveSource.Dispose();
            this.soundOut.Dispose();
        });
    }

    private void CreateSoundOut()
    {
        this.remainingTimesToLoop--;
        this.waveSource = CodecFactory.Instance.GetCodec(this.soundFile);
        this.soundOut = new ();
        this.soundOut.Initialize(this.waveSource);
        this.soundOut.Volume = this.volume;
        this.soundOut.Stopped += this.OnStopped;
        this.soundOut.Play();
    }
}
