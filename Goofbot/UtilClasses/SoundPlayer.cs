namespace Goofbot.Utils;

using System.Threading.Tasks;
using CSCore.Codecs;
using CSCore;
using CSCore.SoundOut;
using System.IO;
using System;
using System.Threading;

internal class SoundPlayer : IDisposable
{
    public EventHandler Disposed;

    private const float DefaultVolume = 0.15f;

    private readonly string soundFile;
    private readonly float volume;

    private readonly object lockObject = new ();
    private readonly IWaveSource waveSource;
    private readonly WasapiOut soundOut;
    private readonly CancellationToken? cancellationToken;

    public SoundPlayer(string soundFile, float volume = DefaultVolume, CancellationToken? cancellationToken = null, bool playImmediately = true)
    {
        this.IsDisposed = true;

        if (File.Exists(soundFile))
        {
            this.IsDisposed = false;
            this.soundFile = soundFile;
            this.volume = volume;

            this.waveSource = CodecFactory.Instance.GetCodec(this.soundFile);

            this.soundOut = new ();
            this.soundOut.Initialize(this.waveSource);
            this.soundOut.Volume = this.volume;
            this.soundOut.Stopped += this.OnStopped;

            this.cancellationToken = cancellationToken;
            this.cancellationToken?.Register(this.Dispose);

            if (playImmediately)
            {
                this.Play();
            }
        }
    }

    public bool IsDisposed { get; private set; }

    public void Play()
    {
        this.soundOut.Play();
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

                this.IsDisposed = true;
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
