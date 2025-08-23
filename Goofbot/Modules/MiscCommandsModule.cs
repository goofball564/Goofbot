namespace Goofbot.Modules;

using Goofbot.Utils;
using System;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class MiscCommandsModule : GoofbotModule
{
    private readonly Random random = new ();

    public MiscCommandsModule(string moduleDataFolder, CommandDictionary commandDictionary)
        : base(moduleDataFolder)
    {
        commandDictionary.TryAddCommand(new ("antici", this.AnticiCommand, 1));
    }

    public async Task<string> AnticiCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs)
    {
        int randomDelay = this.random.Next(50000) + 10000;
        await Task.Delay(randomDelay);

        string username = eventArgs.Command.ChatMessage.DisplayName;
        return $"...pation! @{username}";
    }
}
