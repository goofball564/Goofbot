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

    private readonly string emoteListFile;

    private readonly Dictionary<string, string> emoteSoundDictionary = [];

    public EmoteSoundModule(string moduleDataFolder, TwitchClient twitchClient)
        : base(moduleDataFolder)
    {
        this.emoteListFile = Path.Join(this.ModuleDataFolder, EmoteListFileName);
        this.ParseTheThing();

        twitchClient.OnMessageReceived += this.Client_OnMessageReceived;
    }

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

    private async void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
    {
        string message = e.ChatMessage.Message;
        Stopwatch stopwatch = new ();
        stopwatch.Start();

        foreach (Match match in WordRegex().Matches(message))
        {
            if (this.emoteSoundDictionary.TryGetValue(match.Value, out string soundFile))
            {
                await Task.Delay(500 - (int)stopwatch.ElapsedMilliseconds);
                new SoundPlayer(soundFile, volume: 0.075f);
                stopwatch.Restart();
            }
        }
    }
}
