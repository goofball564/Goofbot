namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;

internal partial class EmoteSoundModule : GoofbotModule
{
    private const string EmoteListFileName = "emotes.txt";
    private const int SoundIntervalInMilliseconds = 500;
    private const float Volume = 0.075f;

    private readonly string emoteListFile;

    private readonly Dictionary<string, string> emoteSoundDictionary = [];

    public EmoteSoundModule(string moduleDataFolder, TwitchClient twitchClient)
        : base(moduleDataFolder)
    {
        this.emoteListFile = Path.Join(this.ModuleDataFolder, EmoteListFileName);
        this.ParseTheThing();

        twitchClient.OnMessageReceived += this.Client_OnMessageReceived;
    }

    /*
     * Regex matches each word in a string.
     * (A word is all non-whitespace characters between word boundaries,
     * assuming there's at least one character).
     */
    [GeneratedRegex("(?<=\\b)\\S+(?=\\b)")]
    private static partial Regex WordRegex();

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

        foreach (Match match in WordRegex().Matches(message))
        {
            if (this.emoteSoundDictionary.TryGetValue(match.Value, out string soundFile))
            {
                await delayTask;
                new SoundPlayer(soundFile, volume: Volume);
                delayTask = Task.Delay(SoundIntervalInMilliseconds);
            }
        }
    }
}
