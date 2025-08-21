namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TwitchLib.Client;
using TwitchLib.Client.Events;

internal partial class EmoteSoundModule : GoofbotModule
{
    private const string LizardFileName = "lizard.wav";

    private readonly string lizardFile;

    public EmoteSoundModule(string moduleDataFolder, TwitchClient twitchClient)
        : base(moduleDataFolder)
    {
        this.lizardFile = Path.Join(this.ModuleDataFolder, LizardFileName);
        twitchClient.OnMessageReceived += this.Client_OnMessageReceived;
    }

    [GeneratedRegex("(?<=\\b)LIZARD(?=\\b)")]
    private static partial Regex LizardRegex();

    private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
    {
        string message = e.ChatMessage.Message.Trim();
        int lizardCount = LizardRegex().Count(message);
        new SoundPlayer(this.lizardFile, numberTimesToLoop: lizardCount);
    }
}
