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
        public MiscCommandsModule(string moduleDataFolder, CommandDictionary commandDictionary) : base(moduleDataFolder)
        {
            var anticiCommandLambda = async (object module, string commandArgs, OnMessageReceivedArgs messageArgs) => { return await MiscCommandsModule.AnticiCommand(messageArgs); };
            commandDictionary.TryAddCommand(new("!antici", this, anticiCommandLambda, 1));
        }

        public static async Task<string> AnticiCommand(OnMessageReceivedArgs e)
        {
            Console.WriteLine("Hello?");
            Random random = new();
            string username = e.ChatMessage.DisplayName;
            await Task.Delay(15000);
            return $"...pation! @{username}";
        }
    }
}
