namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class MiscCommandsModule : GoofbotModule
{
    private const string CommandsCommandName = "commands";

    private readonly Random random = new ();

    public MiscCommandsModule(string moduleDataFolder)
        : base(moduleDataFolder)
    {
        Program.CommandDictionary.TryAddCommand(new ("antici", this.AnticiCommand, 1));
        Program.CommandDictionary.TryAddCommand(new (CommandsCommandName, this.CommandsCommand, 1));
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
        List<string> commandNames = [];

        foreach (var dictionaryEntry in Program.CommandDictionary)
        {
            if (dictionaryEntry.Value.CommandAccessibilityModifier == CommandAccessibilityModifier.StreamerOnly)
            {
                continue;
            }

            string commandName = dictionaryEntry.Key;

            if (commandName.Equals(CommandsCommandName))
            {
                commandName = Program.ReverseString(commandName);
            }

            commandNames.Add(commandName);
        }


        commandNames.Sort();
        if (isReversed)
        {
            commandNames.Reverse();
        }

        for (int i = 0; i < commandNames.Count; i++)
        {
            string commandAccessibilityModifier = string.Empty;
            if (Program.CommandDictionary.TryGetCommand(commandNames[i], out Command command))
            {
                switch (command.CommandAccessibilityModifier)
                {
                    case CommandAccessibilityModifier.StreamerOnly:
                        commandAccessibilityModifier = "streamer-only";
                        break;
                    case CommandAccessibilityModifier.SubOnly:
                        commandAccessibilityModifier = "sub-only";
                        break;
                    case CommandAccessibilityModifier.ModOnly:
                        commandAccessibilityModifier = "mod-only";
                        break;
                }
            }

            if (isReversed)
            {
                commandNames[i] = commandAccessibilityModifier.Equals(string.Empty) ? $"{commandNames[i]}!" : $"){commandAccessibilityModifier}( {commandNames[i]}!";
            }
            else
            {
                commandNames[i] = commandAccessibilityModifier.Equals(string.Empty) ? $"!{commandNames[i]}" : $"!{commandNames[i]} ({commandAccessibilityModifier})";
            }
        }

        string listOfCommands;

        if (isReversed)
        {
            listOfCommands = string.Join(" ,", commandNames);
        }
        else
        {
            listOfCommands = string.Join(", ", commandNames);
        }

        return listOfCommands;
    }
}
