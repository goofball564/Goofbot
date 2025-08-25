namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;

internal partial class EmoteSoundModule : GoofbotModule
{
    private const int SoundIntervalInMilliseconds = 500;
    private const float Volume = 0.075f;

    private readonly string emoteListFile;

    private readonly Dictionary<string, string> emoteSoundDictionary = [];

    public EmoteSoundModule(string moduleDataFolder, CommandDictionary commandDictionary, ColorDictionary colorDictionary, TwitchClient twitchClient, TwitchAPI twitchAPI)
        : base(moduleDataFolder, commandDictionary, colorDictionary, twitchClient, twitchAPI)
    {
        this.emoteListFile = Path.Join(this.ModuleDataFolder, "emotes.txt");
        this.ParseTheThing();

        twitchClient.OnMessageReceived += this.Client_OnMessageReceived;
    }

    private void ParseTheThing()
    {
        string soundFile = string.Empty;
        foreach (string line in File.ReadLines(this.emoteListFile))
        {
            if (line.Equals(string.Empty))
            {
                soundFile = string.Empty;
            }
            else if (soundFile.Equals(string.Empty))
            {
                soundFile = line.Trim();
            }
            else
            {
                this.emoteSoundDictionary.TryAdd(line, Path.Join(this.ModuleDataFolder, soundFile));
            }
        }
    }

    /*
     * Play a sound for every matching word (emote) in a message. There is an interval of time
     * between each sound being played.
     */
    private async void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
    {
        string message = e.ChatMessage.Message;
        Task delayTask = Task.Delay(SoundIntervalInMilliseconds);

        foreach (string word in message.Split(null as char[], StringSplitOptions.RemoveEmptyEntries))
        {
            if (this.emoteSoundDictionary.TryGetValue(word, out string soundFile))
            {
                await delayTask;
                new SoundPlayer(soundFile, volume: Volume);
                delayTask = Task.Delay(SoundIntervalInMilliseconds);
            }
        }
    }
}
