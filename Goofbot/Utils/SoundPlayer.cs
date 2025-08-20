namespace Goofbot.Utils;

using System.Threading.Tasks;
using CSCore.Codecs;
using CSCore;
using CSCore.SoundOut;

internal class SoundPlayer
{
    private const float DefaultVolume = 0.15f;

    private readonly ISoundOut soundOut;
    private readonly IWaveSource waveSource;

    public SoundPlayer(string soundFile)
    {
        this.waveSource = CodecFactory.Instance.GetCodec(soundFile);

        this.soundOut = new WasapiOut();
        this.soundOut.Initialize(this.waveSource);
        this.soundOut.Volume = DefaultVolume;
        this.soundOut.Stopped += this.OnStopped;
    }

    public void Play()
    {
         this.soundOut.Play();
    }

    public void OnStopped(object sender, PlaybackStoppedEventArgs e)
    {
        Task.Run(() =>
        {
            this.soundOut.Dispose();
            this.waveSource.Dispose();
        });
    }
}
