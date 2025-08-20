using Goofbot.Utils;
using System;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace Goofbot.Modules
{
    internal class MiscCommandsModule : GoofbotModule
    {
        private readonly Random _random = new();

        public MiscCommandsModule(string moduleDataFolder, CommandDictionary commandDictionary) : base(moduleDataFolder)
        {
            var anticiCommandLambda = async (object module, string commandArgs, OnChatCommandReceivedArgs eventArgs) => { return await ((MiscCommandsModule)module).AnticiCommand(eventArgs); };
            commandDictionary.TryAddCommand(new("antici", this, anticiCommandLambda, 1));
        }

        public async Task<string> AnticiCommand(OnChatCommandReceivedArgs e)
        {
            int randomDelay = _random.Next(50000) + 10000;
            await Task.Delay(randomDelay);

            string username = e.Command.ChatMessage.DisplayName;
            return $"...pation! @{username}";
        }
    }
}
