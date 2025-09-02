namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

internal class PipeServerModule
{
    private Thread listenerThread;
    private CancellationTokenSource listenerCancellationSource;

    public PipeServerModule()
    {
    }

    ~PipeServerModule()
    {
        this.Stop();
    }

    public event EventHandler<int> RunStart;

    public event EventHandler<int> RunReset;

    public event EventHandler<RunSplitEventArgs> RunSplit;

    public event EventHandler RunGold;

    public void Start()
    {
        if (this.listenerThread == null)
        {
            this.listenerCancellationSource = new CancellationTokenSource();
            ThreadStart threadStart = new (() => this.Listen(this.listenerCancellationSource.Token));
            this.listenerThread = new (threadStart)
            {
                IsBackground = true,
            };
            this.listenerThread.Start();
        }
    }

    public void Stop()
    {
        if (this.listenerThread != null)
        {
            this.listenerCancellationSource.Cancel();
            this.listenerThread = null;
            this.listenerCancellationSource.Dispose();
            this.listenerCancellationSource = null;
        }
    }

    protected virtual void OnRunStart(int runCount)
    {
        this.RunStart?.Invoke(this, runCount);
    }

    protected virtual void OnRunReset(int runCount)
    {
        this.RunReset?.Invoke(this, runCount);
    }

    protected virtual void OnRunSplit(RunSplitEventArgs e)
    {
        this.RunSplit?.Invoke(this, e);
    }

    protected virtual void OnRunGold()
    {
        this.RunGold?.Invoke(this, new EventArgs());
    }

    private static string ProcessSingleIncomingMessage(NamedPipeServerStream namedPipeServer)
    {
        StringBuilder messageBuilder = new ();
        byte[] messageBuffer = new byte[5];
        do
        {
            namedPipeServer.Read(messageBuffer, 0, messageBuffer.Length);
            string messageChunk = Encoding.UTF8.GetString(messageBuffer).Trim('\0');
            messageBuilder.Append(messageChunk);
            messageBuffer = new byte[messageBuffer.Length];
        }
        while (!namedPipeServer.IsMessageComplete);
        return messageBuilder.ToString();
    }

    private void Listen(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using (var namedPipeServer = new NamedPipeServerStream("Goofbot", PipeDirection.InOut, 1, PipeTransmissionMode.Message))
            {
                namedPipeServer.WaitForConnection();
                string message = ProcessSingleIncomingMessage(namedPipeServer);
                string[] words = message.Split(' ');
                switch (words[0])
                {
                    case "Start":
                        if (words.Length >= 2 && int.TryParse(words[1], out int runCountStart))
                        {
                            this.OnRunStart(runCountStart);
                        }

                        break;
                    case "Reset":
                        if (words.Length >= 2 && int.TryParse(words[1], out int runCountReset))
                        {
                            this.OnRunReset(runCountReset);
                        }

                        break;
                    case "Split":
                        if (words.Length >= 3 && int.TryParse(words[1], out int currentSplitIndex) && int.TryParse(words[1], out int segmentCount))
                        {
                            RunSplitEventArgs e = new ()
                            {
                                CurrentSplitIndex = currentSplitIndex,
                                SegmentCount = segmentCount,
                            };
                            this.OnRunSplit(e);
                        }

                        break;
                    /*case "Gold":
                        Console.WriteLine("Hooray!");
                        OnRunGold();
                        break;*/
                    default:
                        break;
                }
            }
        }
    }
}
