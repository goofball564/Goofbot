namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;

internal class MiscCommandsModule : GoofbotModule
{
    private readonly Random random = new ();

    public MiscCommandsModule(string moduleDataFolder)
        : base(moduleDataFolder)
    {
        Program.CommandDictionary.TryAddCommand(new ("antici", this.AnticiCommand, 1));
        Program.CommandDictionary.TryAddCommand(new ("commands", this.CommandsCommand, 1));
    }

    public async Task<string> AnticiCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        int randomDelay = this.random.Next(50000) + 10000;
        await Task.Delay(randomDelay);

        string username = eventArgs.Command.ChatMessage.DisplayName;
        return $"...pation! @{username}";
    }

    public async Task<string> CommandsCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        List<string> commands = [];

        foreach (var dictionaryEntry in Program.CommandDictionary)
        {
            string commandName = dictionaryEntry.Key;
            string commandAccessibilityModifier = string.Empty;
            switch (dictionaryEntry.Value.CommandAccessibilityModifier)
            {
                case CommandAccessibilityModifier.StreamerOnly:
                    continue;
                case CommandAccessibilityModifier.SubOnly:
                    commandAccessibilityModifier = " (sub-only)";
                    break;
                case CommandAccessibilityModifier.ModOnly:
                    commandAccessibilityModifier = " (mod-only)";
                    break;
            }

            if (isReversed)
            {
                commands.Add($"{Program.ReverseString(commandAccessibilityModifier)}{commandName}!");
            }
            else
            {
                commands.Add($"!{commandName}{commandAccessibilityModifier}");
            }
        }

        commands.Sort();
        string listOfCommands;

        if (isReversed)
        {
            listOfCommands = string.Join(" ,", commands);
        }
        else
        {
            listOfCommands = string.Join(", ", commands);
        }

        return listOfCommands;
    }
}
