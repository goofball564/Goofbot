namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class MiscCommandsModule : GoofbotModule
{
    private readonly Random random = new ();
    private readonly CommandDictionary commandDictionary;

    public MiscCommandsModule(string moduleDataFolder, CommandDictionary commandDictionary)
        : base(moduleDataFolder)
    {
        this.commandDictionary = commandDictionary;
        this.commandDictionary.TryAddCommand(new ("antici", this.AnticiCommand, 1));
        this.commandDictionary.TryAddCommand(new ("commands", this.CommandsCommand, 1));
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
        List<string> commands = this.commandDictionary.GetAllCommands();
        commands.Sort();

        string listOfCommands;

        if (isReversed)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                commands[i] += "!";
            }

            listOfCommands = string.Join(" ,", commands);
        }
        else
        {
            for (int i = 0; i < commands.Count; i++)
            {
                commands[i] = "!" + commands[i];
            }

            listOfCommands = string.Join(", ", commands);
        }

        return listOfCommands;
    }
}
