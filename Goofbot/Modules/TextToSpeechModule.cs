namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

internal class TextToSpeechModule : GoofbotModule
{
    private const int Volume = 60;

    private readonly BlockingCollection<QueuedTTS> ttsQueue = new (new ConcurrentQueue<QueuedTTS>(), 1000);
    private QueuedTTS currentTTS;

    public TextToSpeechModule(string moduleDataFolder)
        : base(moduleDataFolder)
    {
        this.currentTTS = new (string.Empty, string.Empty, this.SpeakSAPI5);
        Task runningTask = Task.Run(async () =>
        {
            while (true)
            {
                this.currentTTS = this.ttsQueue.Take();

                try
                {
                    await this.currentTTS.Execute();
                }
                catch
                {
                }
                finally
                {
                    this.currentTTS.Dispose();
                }
            }
        });

        Program.EventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += this.OnChannelPointsCustomRewardRedemptionAdd;

        Program.CommandDictionary.TryAddCommand(new Command("tts", this.TTSCommand, 1, CommandAccessibilityModifier.SubOnly));
        Program.CommandDictionary.TryAddCommand(new Command("cancel", this.CancelCommand, 0, CommandAccessibilityModifier.StreamerOnly));
    }

    public async Task<string> TTSCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        if (commandArgs.Equals(string.Empty))
        {
            return "Enter a message with this command to hear it read aloud by the inimitable Microsoft Sam";
        }
        else
        {
            string username = eventArgs.Command.ChatMessage.DisplayName.ToLowerInvariant();
            this.ttsQueue.Add(new QueuedTTS(username, commandArgs, this.SpeakSAPI5));
            return string.Empty;
        }
    }

    public async Task<string> CancelCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        switch (commandArgs)
        {
            case "all":
                foreach (QueuedTTS tts in this.ttsQueue)
                {
                    tts.TryCancel();
                }

                this.currentTTS.TryCancel();

                break;
            case "":
                this.currentTTS.TryCancel();

                break;
            default:
                foreach (QueuedTTS tts in this.ttsQueue)
                {
                    if (tts.Username.Equals(commandArgs))
                    {
                        tts.TryCancel();
                    }
                }

                if (this.currentTTS.Username.Equals(commandArgs))
                {
                    this.currentTTS.TryCancel();
                }

                break;
        }

        return string.Empty;
    }

    private static SpeechSynthesizer InitializeSpeechSynthesizer()
    {
        SpeechSynthesizer speechSynthesizer = new ();
        speechSynthesizer.SetOutputToDefaultAudioDevice();
        speechSynthesizer.Volume = Volume;
        return speechSynthesizer;
    }

    private async Task SpeakSAPI5(string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(2000, cancellationToken);
        using (SpeechSynthesizer speechSynthesizer = InitializeSpeechSynthesizer())
        {
            Prompt speechPrompt = speechSynthesizer.SpeakAsync(message);
            while (!(speechPrompt.IsCompleted || cancellationToken.IsCancellationRequested))
            {
                await Task.Delay(500, cancellationToken);
            }
        }
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        if (e.Notification.Payload.Event.Reward.Title.Equals("TTS"))
        {
            string message = e.Notification.Payload.Event.UserInput;
            string username = e.Notification.Payload.Event.UserName.ToLowerInvariant();

            this.ttsQueue.Add(new QueuedTTS(username, message, this.SpeakSAPI5));
        }
    }
}

internal class QueuedTTS : IDisposable
{
    public QueuedTTS(string username, string message, Func<string, CancellationToken, Task> action)
    {
        this.Username = username;
        this.Message = message;
        this.Action = action;

        this.CancellationTokenSource = new ();
    }

    public string Username { get; private set; }

    public string Message { get; private set; }

    public CancellationTokenSource CancellationTokenSource { get; private set; }

    public Func<string, CancellationToken, Task> Action { get; private set; }

    public async Task Execute()
    {
        await this.Action(this.Message, this.CancellationTokenSource.Token);
    }

    public void TryCancel()
    {
        try
        {
            this.CancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        this.CancellationTokenSource.Dispose();
    }
}
