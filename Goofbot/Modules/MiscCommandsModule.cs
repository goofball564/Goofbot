namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class MiscCommandsModule : GoofbotModule
{
    private const string CommandsCommandName = "commands";
    private const int AnticiCommandMinimumDelay = 10000;
    private const int AnticiCommandDelayVariance = 50000;

    private readonly Random random = new ();

    public MiscCommandsModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.bot.CommandDictionary.TryAddCommand(new ("antici", this.AnticiCommand, timeoutSeconds: 0));
        this.bot.CommandDictionary.TryAddCommand(new (CommandsCommandName, this.CommandsCommand));

        this.bot.CommandDictionary.TryAddCommand(new ("lock", this.LockCommand, CommandAccessibilityModifier.StreamerOnly));
        this.bot.CommandDictionary.TryAddCommand(new ("unlock", this.UnlockCommand, CommandAccessibilityModifier.StreamerOnly));
    }

    public async Task LockCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        if (this.bot.CommandDictionary.TryGetCommand(commandArgs, out Command command))
        {
            if (command.TryLock())
            {
                this.bot.SendMessage($"!{commandArgs} command has been locked!", isReversed);
            }
            else
            {
                this.bot.SendMessage($"!{commandArgs} can't be locked", isReversed);
            }
        }
        else
        {
            this.bot.SendMessage($"!{commandArgs} doesn't exist", isReversed);
        }
    }

    public async Task UnlockCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        if (this.bot.CommandDictionary.TryGetCommand(commandArgs, out Command command))
        {
            if (command.TryUnlock())
            {
                this.bot.SendMessage($"!{commandArgs} command has been unlocked!", isReversed);
            }
            else
            {
                this.bot.SendMessage($"!{commandArgs} can't be unlocked", isReversed);
            }
        }
        else
        {
            this.bot.SendMessage($"!{commandArgs} doesn't exist", isReversed);
        }
    }

    public async Task AnticiCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        int randomDelay = this.random.Next(AnticiCommandDelayVariance) + AnticiCommandMinimumDelay;
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
