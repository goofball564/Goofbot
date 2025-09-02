namespace Goofbot.Modules;

using Goofbot.Utils;
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

    private readonly SemaphoreSlim speechSemaphore = new (1, 1);

    private SpeechSynthesizer speechSynthesizer;

    public TextToSpeechModule(string moduleDataFolder)
        : base(moduleDataFolder)
    {
        this.speechSynthesizer = new ();
        this.speechSynthesizer.SetOutputToDefaultAudioDevice();
        this.speechSynthesizer.Volume = Volume;

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
        if (commandArgs.Equals(string.Empty))
        {
            return "Enter a message with this command to hear it read aloud by the inimitable Microsoft Sam";
        }
        else
        {
            await this.SpeakAsync(commandArgs);
            return string.Empty;
        }
    }

    public async Task<string> EmergencyStopCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        this.speechSynthesizer.Dispose();
        this.speechSynthesizer = new ();
        this.speechSynthesizer.SetOutputToDefaultAudioDevice();
        this.speechSynthesizer.Volume = Volume;

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
