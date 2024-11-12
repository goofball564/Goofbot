using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace Goofbot.Modules
{
    internal class PipeServerModule
    {
        public event EventHandler<int> RunStart;
        public event EventHandler<int> RunReset;
        public event EventHandler<RunSplitEventArgs> RunSplit;
        public event EventHandler RunGold;

        private Thread ListenerThread;
        private CancellationTokenSource ListenerCancellationSource;

        protected virtual void OnRunStart(int runCount)
        {
            RunStart?.Invoke(this, runCount);
        }

        protected virtual void OnRunReset(int runCount)
        {
            RunReset?.Invoke(this, runCount);
        }

        protected virtual void OnRunSplit(RunSplitEventArgs e)
        {
            RunSplit?.Invoke(this, e);
        }

        protected virtual void OnRunGold()
        {
            RunGold?.Invoke(this, new EventArgs());
        }

        public PipeServerModule()
        {

        }

        ~PipeServerModule()
        {
            Stop();
        }

        public void Start()
        {
            if (ListenerThread == null)
            {
                ListenerCancellationSource = new CancellationTokenSource();
                var threadStart = new ThreadStart(() => Listen(ListenerCancellationSource.Token));
                ListenerThread = new Thread(threadStart);
                ListenerThread.IsBackground = true;
                ListenerThread.Start();
            }
        }

        public void Stop()
        {
            if (ListenerThread != null)
            {
                ListenerCancellationSource.Cancel();
                ListenerThread = null;
                ListenerCancellationSource.Dispose();
                ListenerCancellationSource = null;
            }
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
                            if (words.Length >= 2 && Int32.TryParse(words[1], out int runCountStart))
                                OnRunStart(runCountStart);
                            break;
                        case "Reset":
                            if (words.Length >= 2 && Int32.TryParse(words[1], out int runCountReset))
                                OnRunReset(runCountReset);
                            break;
                        case "Split":
                            if (words.Length >= 3 && Int32.TryParse(words[1], out int currentSplitIndex) && Int32.TryParse(words[1], out int segmentCount))
                            {
                                var e = new RunSplitEventArgs();
                                e.CurrentSplitIndex = currentSplitIndex;
                                e.SegmentCount = segmentCount;
                                OnRunSplit(e);
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

        private static string ProcessSingleIncomingMessage(NamedPipeServerStream namedPipeServer)
        {
            StringBuilder messageBuilder = new StringBuilder();
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
    }

}

internal class RunSplitEventArgs : EventArgs
{
    public int CurrentSplitIndex { get; set; }
    public int SegmentCount { get; set; }
}
