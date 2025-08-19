using System.IO;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;

namespace Goofbot
{
    internal abstract class GoofbotModule
    {
        protected readonly string _moduleDataFolder;
        protected readonly TwitchClient _twitchClient;
        protected readonly TwitchAPI _twitchAPI;
        protected GoofbotModule(string moduleDataFolder, TwitchClient twitchClient, TwitchAPI twitchAPI) 
        {
            _moduleDataFolder = Path.Combine(Program.StuffFolder, moduleDataFolder);
            _twitchClient = twitchClient;
            _twitchAPI = twitchAPI;

            _twitchClient.OnMessageReceived += Client_OnMessageReceived;
        }

        protected abstract void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e);
    }
}
