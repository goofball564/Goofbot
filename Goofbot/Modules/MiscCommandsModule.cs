using Goofbot.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace Goofbot.Modules
{
    internal class MiscCommandsModule : GoofbotModule
    {
        private Random _random = new();

        public MiscCommandsModule(string moduleDataFolder, CommandDictionary commandDictionary) : base(moduleDataFolder)
        {
            var anticiCommandLambda = async (object module, string commandArgs, OnMessageReceivedArgs messageArgs) => { return await ((MiscCommandsModule)module)AnticiCommand(messageArgs); };
            commandDictionary.TryAddCommand(new("!antici", this, anticiCommandLambda, 1));
        }

        public async Task<string> AnticiCommand(OnMessageReceivedArgs e)
        {
            int randomDelay = _random.Next(50000) + 10000;
            await Task.Delay(randomDelay);

            string username = e.ChatMessage.DisplayName;
            return $"...pation! @{username}";
        }
    }
}
