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

    public MiscCommandsModule(string moduleDataFolder, CommandDictionary commandDictionary, ColorDictionary colorDictionary, TwitchClient twitchClient, TwitchAPI twitchAPI)
        : base(moduleDataFolder, commandDictionary, colorDictionary, twitchClient, twitchAPI)
    {
        this.CommandDictionary.TryAddCommand(new ("antici", this.AnticiCommand, 1));
        this.CommandDictionary.TryAddCommand(new ("commands", this.CommandsCommand, 1));
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
        List<string> commands = this.CommandDictionary.GetAllCommands();
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
