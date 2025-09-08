namespace Goofbot.Utils;

using System.Threading.Tasks;
using CSCore.Codecs;
using CSCore;
using CSCore.SoundOut;
using System.IO;
using System;

internal class SoundPlayer : IDisposable
{
    private object lockObject = new ();
    private const float DefaultVolume = 0.15f;

    private readonly string soundFile;
    private readonly float volume;

    private IWaveSource waveSource;
    private WasapiOut soundOut;

    private volatile bool isDisposed = true;

    public SoundPlayer(string soundFile, float volume = DefaultVolume)
    {
        if (File.Exists(soundFile))
        {
            this.isDisposed = false;
            this.soundFile = soundFile;
            this.volume = volume;
            this.waveSource = CodecFactory.Instance.GetCodec(this.soundFile);
            this.soundOut = new ();
            this.soundOut.Initialize(this.waveSource);
            this.soundOut.Volume = this.volume;
            this.soundOut.Stopped += this.OnStopped;
            this.soundOut.Play();
        }
    }

    public bool IsDisposed
    {
        get { return this.isDisposed;  }
    }

    public void Dispose()
    {
        lock (this.lockObject)
        {
            if (!this.IsDisposed)
            {
                try
                {
                    this.soundOut.Dispose();
                }
                catch
                {
                }

                try
                {
                    this.waveSource.Dispose();
                }
                catch
                {
                }

                this.isDisposed = true;
            }
        }
    }

    private async void OnStopped(object sender, PlaybackStoppedEventArgs e)
    {
        await Task.Run(() =>
        {
            this.Dispose();
        });
    }
}
