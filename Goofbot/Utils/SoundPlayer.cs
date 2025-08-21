namespace Goofbot.Utils;

using System.Threading.Tasks;
using CSCore.Codecs;
using CSCore;
using CSCore.SoundOut;

internal class SoundPlayer
{
    private const float DefaultVolume = 0.15f;

    private readonly WasapiOut soundOut;
    private readonly IWaveSource waveSource;

    public SoundPlayer(string soundFile, float volume = DefaultVolume, int numberTimesToLoop = 1)
    {
        this.waveSource = CodecFactory.Instance.GetCodec(soundFile);

        this.soundOut = new ();
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
