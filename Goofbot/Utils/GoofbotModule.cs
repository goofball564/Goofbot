using System.IO;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;

namespace Goofbot.Utils
{
    internal abstract class GoofbotModule
    {
        protected readonly string _moduleDataFolder;
        protected GoofbotModule(string moduleDataFolder) 
        {
            _moduleDataFolder = Path.Combine(Program.StuffFolder, moduleDataFolder);
        }
    }
}
