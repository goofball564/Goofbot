namespace Goofbot.Utils;

using System.Threading.Tasks;
using CSCore.Codecs;
using CSCore;
using CSCore.SoundOut;
using System;
using System.Threading;

internal class SoundPlayer : IDisposable
{
    public EventHandler Disposed;

    private const float DefaultVolume = 0.15f;

    private readonly object lockObject = new ();
    private readonly IWaveSource waveSource;
    private readonly WasapiOut soundOut;
    private readonly CancellationToken? cancellationToken;

    private bool isDisposed = false;

    public SoundPlayer(string soundFile, float volume = DefaultVolume, CancellationToken? cancellationToken = null, bool playImmediately = true)
    {
        try
        {
            this.waveSource = CodecFactory.Instance.GetCodec(soundFile);

            this.soundOut = new ();
            this.soundOut.Initialize(this.waveSource);
            this.soundOut.Volume = volume;
            this.soundOut.Stopped += this.OnStopped;

            this.cancellationToken = cancellationToken;

            if (playImmediately)
            {
                this.Play();
            }
        }
        catch
        {
            if (playImmediately)
            {
                this.Dispose();
            }
        }
    }

    public void Play()
    {
        this.cancellationToken?.Register(this.Dispose);
        try
        {
            this.soundOut.Play();
        }
        catch
        {
            this.Dispose();
        }
    }

    public void Dispose()
    {
        lock (this.lockObject)
        {
            if (!this.isDisposed)
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
                this.Disposed.Invoke(this, new EventArgs());
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
