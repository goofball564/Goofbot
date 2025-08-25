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

[SupportedOSPlatform("windows")]
internal class TextToSpeechModule : GoofbotModule
{
    private readonly SpeechSynthesizer speechSynthesizer = new ();
    private readonly SemaphoreSlim speechSemaphore = new (1, 1);

    public TextToSpeechModule(string moduleDataFolder, CommandDictionary commandDictionary, ColorDictionary colorDictionary, TwitchClient twitchClient, TwitchAPI twitchAPI)
        : base(moduleDataFolder, commandDictionary, colorDictionary, twitchClient, twitchAPI)
    {
        this.speechSynthesizer.SetOutputToDefaultAudioDevice();

        this.CommandDictionary.TryAddCommand(new Command("tts", this.TTSCommand, 1, CommandAccessibilityModifier.SubOnly));
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

    private async Task SpeakAsync(string messageToSpeak)
    {
        await this.speechSemaphore.WaitAsync();
        try
        {
            await Task.Run(() => this.speechSynthesizer.Speak(messageToSpeak));
        }
        finally
        {
            this.speechSemaphore.Release();
        }
    }
}
