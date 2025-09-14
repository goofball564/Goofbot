namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class MiscCommandsModule : GoofbotModule
{
    private const string CommandsCommandName = "commands";

    private readonly Random random = new ();

    public MiscCommandsModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.bot.CommandDictionary.TryAddCommand(new ("antici", this.AnticiCommand, timeoutSeconds: 0));
        this.bot.CommandDictionary.TryAddCommand(new (CommandsCommandName, this.CommandsCommand));
    }

    public async Task AnticiCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        int randomDelay = this.random.Next(50000) + 10000;
        await Task.Delay(randomDelay);

        string username = eventArgs.Command.ChatMessage.DisplayName;
        this.bot.SendMessage($"...pation! @{username}", isReversed);
    }

    public Task CommandsCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        List<string> commandNames = [];

        foreach (var dictionaryEntry in this.bot.CommandDictionary)
        {
            if (dictionaryEntry.Value.CommandAccessibilityModifier == CommandAccessibilityModifier.StreamerOnly || dictionaryEntry.Value.Unlisted)
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
            if (this.bot.CommandDictionary.TryGetCommand(commandNames[i], out Command command))
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

        this.bot.SendMessage(listOfCommands, isReversed);
        return Task.Delay(0);
    }
}
