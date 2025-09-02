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

[SupportedOSPlatform("windows")]
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
                await this.currentTTS.Execute();
            }
        });

        Program.EventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += this.OnChannelPointsCustomRewardRedemptionAdd;

        Program.CommandDictionary.TryAddCommand(new Command("tts", this.TTSCommand, 1, CommandAccessibilityModifier.SubOnly));
        Program.CommandDictionary.TryAddCommand(new Command("emergencystop", this.EmergencyStopCommand, 0, CommandAccessibilityModifier.StreamerOnly));
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

    public async Task<string> EmergencyStopCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        foreach (QueuedTTS tts in this.ttsQueue)
        {
            tts.CancellationTokenSource.Cancel();
        }

        this.currentTTS.CancellationTokenSource.Cancel();

        return string.Empty;
    }

    private SpeechSynthesizer InitializeSpeechSynthesizer()
    {
        SpeechSynthesizer speechSynthesizer = new ();
        speechSynthesizer.SetOutputToDefaultAudioDevice();
        speechSynthesizer.Volume = Volume;
        return speechSynthesizer;
    }

    private async Task SpeakSAPI5(string message, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await Task.Delay(2000);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        using (SpeechSynthesizer speechSynthesizer = this.InitializeSpeechSynthesizer())
        {
            Prompt speechPrompt = speechSynthesizer.SpeakAsync(message);
            while (!(speechPrompt.IsCompleted || cancellationToken.IsCancellationRequested))
            {
                await Task.Delay(500);
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

internal class QueuedTTS
{
    public QueuedTTS(string username, string message, Func<string, CancellationToken, Task> action)
    {
        this.Username = username;
        this.Message = message;
        this.Action = action;

        this.CancellationTokenSource = new ();
    }

    ~QueuedTTS()
    {
        this.CancellationTokenSource.Dispose();
    }

    public string Username { get; private set; }

    public string Message { get; private set; }

    public CancellationTokenSource CancellationTokenSource { get; private set; }

    public Func<string, CancellationToken, Task> Action { get; private set; }

    public async Task Execute()
    {
        await this.Action(this.Message, this.CancellationTokenSource.Token);
    }
}
