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
        var anticiCommandLambda = async (object module, string commandArgs, OnChatCommandReceivedArgs eventArgs) => { return await ((MiscCommandsModule)module).AnticiCommand(eventArgs); };
        commandDictionary.TryAddCommand(new ("antici", this, anticiCommandLambda, 1));
    }

    public async Task<string> AnticiCommand(OnChatCommandReceivedArgs e)
    {
        int randomDelay = this.random.Next(50000) + 10000;
        await Task.Delay(randomDelay);

        string username = e.Command.ChatMessage.DisplayName;
        return $"...pation! @{username}";
    }
}
