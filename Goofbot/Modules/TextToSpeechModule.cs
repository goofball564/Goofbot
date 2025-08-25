namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

[SupportedOSPlatform("windows")]
internal class TextToSpeechModule : GoofbotModule
{
    private readonly SemaphoreSlim speechSemaphore = new (1, 1);

    private SpeechSynthesizer speechSynthesizer;

    public TextToSpeechModule(string moduleDataFolder)
        : base(moduleDataFolder)
    {
        this.speechSynthesizer = new ();
        this.speechSynthesizer.SetOutputToDefaultAudioDevice();

        Program.EventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += this.OnChannelPointsCustomRewardRedemptionAdd;

        Program.CommandDictionary.TryAddCommand(new Command("tts", this.TTSCommand, 1, CommandAccessibilityModifier.SubOnly));
        Program.CommandDictionary.TryAddCommand(new Command("emergencystop", this.EmergencyStopCommand, 0, CommandAccessibilityModifier.StreamerOnly));
    }

    ~TextToSpeechModule()
    {
        this.speechSynthesizer.Dispose();
    }

    public async Task<string> TTSCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        await this.SpeakAsync(commandArgs);
        return string.Empty;
    }

    public async Task<string> EmergencyStopCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        this.speechSynthesizer.Dispose();
        this.speechSynthesizer = new ();
        this.speechSynthesizer.SetOutputToDefaultAudioDevice();

        return string.Empty;
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        if (e.Notification.Payload.Event.Reward.Title.Equals("TTS"))
        {
            string messageToSpeak = e.Notification.Payload.Event.UserInput;
            await this.SpeakAsync(messageToSpeak);
        }
    }

    private async Task SpeakAsync(string messageToSpeak)
    {
        await this.speechSemaphore.WaitAsync();
        try
        {
            await Task.Delay(2000);
            await Task.Run(() =>
            {
                try
                {
                    this.speechSynthesizer.Speak(messageToSpeak);
                }
                catch
                {
                }
            });
        }
        finally
        {
            this.speechSemaphore.Release();
        }
    }
}
