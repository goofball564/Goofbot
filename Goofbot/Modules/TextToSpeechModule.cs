namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

internal class TextToSpeechModule : GoofbotModule
{
    private const int Volume = 60;
    private const string OutFile = "S:\\speak.WAV";
    private const int PollingPeriodInMilliseconds = 500;
    private const int DelayBeforeTTSInMilliseconds = 2000;

    private readonly string decTalkExeFile;
    private readonly string sapi4ExeFile;
    private readonly Random random = new ();

    private readonly List<Func<string, OnChatCommandReceivedArgs, bool, Task<string>>> listOfTTSCommands = [];

    private readonly BlockingCollection<QueuedTTS> ttsQueue = new (new ConcurrentQueue<QueuedTTS>(), 1000);
    private QueuedTTS currentTTS;

    public TextToSpeechModule(string moduleDataFolder)
        : base(moduleDataFolder)
    {
        this.currentTTS = new (string.Empty, string.Empty, this.SpeakSAPI5);

        this.decTalkExeFile = Path.Join(this.ModuleDataFolder, "DECTalk", "say.exe");
        this.sapi4ExeFile = Path.Join(this.ModuleDataFolder, "BonziBuddyTTS.exe");

        this.listOfTTSCommands.Add(this.SamCommand);
        this.listOfTTSCommands.Add(this.PaulCommand);
        this.listOfTTSCommands.Add(this.BonziCommand);

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

        Program.CommandDictionary.TryAddCommand(new Command("voices", this.VoicesCommand));
        Program.CommandDictionary.TryAddCommand(new Command("tts", this.TTSCommand, CommandAccessibilityModifier.SubOnly));
        Program.CommandDictionary.TryAddCommand(new Command("cancel", this.CancelCommand, CommandAccessibilityModifier.StreamerOnly));

        Program.CommandDictionary.TryAddCommand(new Command("paul", this.PaulCommand, CommandAccessibilityModifier.SubOnly, unlisted: true));
        Program.CommandDictionary.TryAddCommand(new Command("sam", this.SamCommand, CommandAccessibilityModifier.SubOnly, unlisted: true));
        Program.CommandDictionary.TryAddCommand(new Command("bonzi", this.BonziCommand, CommandAccessibilityModifier.SubOnly, unlisted: true));
    }

    public async Task<string> TTSCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        if (commandArgs.Equals(string.Empty))
        {
            return "Enter a message with this command to hear it read by one of Goofbot's TTS !voices";
        }
        else
        {
            // Perform the TTS with a randomly selected TTS voice
            int randomIndex = this.random.Next(this.listOfTTSCommands.Count);
            await this.listOfTTSCommands[randomIndex](commandArgs, eventArgs, isReversed);
            return string.Empty;
        }
    }

    public async Task<string> SamCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        if (commandArgs.Equals(string.Empty))
        {
            string username = Program.TwitchBotUsername;
            string message = "my roflcopter goes soi soi soi soi soi soi my sprinklerststststststststststststststst crotch";
            this.ttsQueue.Add(new QueuedTTS(username, message, this.SpeakSAPI5));
            return message;
        }
        else
        {
            string username = eventArgs.Command.ChatMessage.DisplayName;
            this.ttsQueue.Add(new QueuedTTS(username, commandArgs, this.SpeakSAPI5));
            return string.Empty;
        }
    }

    public async Task<string> PaulCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        if (commandArgs.Equals(string.Empty))
        {
            string username = Program.TwitchBotUsername;
            string message = "aeiou John Madden John Madden John Madden uuuuuuuuuuuuuuuuuuuuu ebrbrbrbrbrbrbrbrbrbrbrbrbrbrbrbrbrbrbrbrbr";
            this.ttsQueue.Add(new QueuedTTS(username, message, this.SpeakDECTalk));
            return message;
        }
        else
        {
            string username = eventArgs.Command.ChatMessage.DisplayName.ToLowerInvariant();
            this.ttsQueue.Add(new QueuedTTS(username, commandArgs, this.SpeakDECTalk));
            return string.Empty;
        }
    }

    public async Task<string> BonziCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        if (commandArgs.Equals(string.Empty))
        {
            return "Enter a message with this command to hear it read by BonziBuddy";
        }
        else
        {
            string username = eventArgs.Command.ChatMessage.DisplayName.ToLowerInvariant();
            this.ttsQueue.Add(new QueuedTTS(username, commandArgs, this.SpeakSAPI4));
            return string.Empty;
        }
    }

    public async Task<string> VoicesCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        if (isReversed)
        {
            return "paul! ,bonzi ,sam!";
        }
        else
        {
            return "!sam, !bonzi, !paul";
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
                    if (tts.Username.Equals(commandArgs, StringComparison.OrdinalIgnoreCase))
                    {
                        tts.TryCancel();
                    }
                }

                if (this.currentTTS.Username.Equals(commandArgs, StringComparison.OrdinalIgnoreCase))
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
        await Task.Delay(DelayBeforeTTSInMilliseconds, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using (SpeechSynthesizer speechSynthesizer = InitializeSpeechSynthesizer())
        {
            Prompt speechPrompt = speechSynthesizer.SpeakAsync(message);
            while (!(speechPrompt.IsCompleted || cancellationToken.IsCancellationRequested))
            {
                await Task.Delay(PollingPeriodInMilliseconds, cancellationToken);
            }
        }
    }

    private async Task SpeakSAPI4(string message, CancellationToken cancellationToken)
    {
        string[] argumentList = { OutFile, message };
        await this.RunProcessThatGeneratesWavThenPlayWav(message, cancellationToken, this.sapi4ExeFile, argumentList);
    }

    private async Task SpeakDECTalk(string message, CancellationToken cancellationToken)
    {
        string[] argumentList = { "-w", OutFile, message };
        await this.RunProcessThatGeneratesWavThenPlayWav(message, cancellationToken, this.decTalkExeFile, argumentList);
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

    private async Task RunProcessThatGeneratesWavThenPlayWav(string message, CancellationToken cancellationToken, string exeFile, string[] argumentList)
    {
        // Cancel if cancellation requested before starting
        cancellationToken.ThrowIfCancellationRequested();
        Task delayTask = Task.Delay(DelayBeforeTTSInMilliseconds, cancellationToken);

        // Run EXE to generate TTS and output it to OutFile
        using (var process = new Process
        {
            StartInfo =
            {
                FileName = exeFile,
                UseShellExecute = false, CreateNoWindow = false,
            },
            EnableRaisingEvents = true,
        })
        {
            foreach (string argument in argumentList)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            await process.WaitForExitAsync(cancellationToken);
        }

        // Wait for a delay before starting to speak, but check if cancelled before speaking
        await delayTask;
        cancellationToken.ThrowIfCancellationRequested();

        // Play TTS from sound file, but stop if cancelled
        using (SoundPlayer soundPlayer = new (OutFile, volume: (float)(Volume / 171.4)))
        {
            while (!(soundPlayer.IsDisposed || cancellationToken.IsCancellationRequested))
            {
                await Task.Delay(PollingPeriodInMilliseconds, cancellationToken);
            }
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
