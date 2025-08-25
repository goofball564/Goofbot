namespace Goofbot.Utils;

using System.IO;
using TwitchLib.Api;
using TwitchLib.Client;

internal abstract class GoofbotModule
{
    protected GoofbotModule(string moduleDataFolder, CommandDictionary commandDictionary, ColorDictionary colorDictionary, TwitchClient twitchClient, TwitchAPI twitchAPI)
    {
        this.ModuleDataFolder = Path.Combine(Program.StuffFolder, moduleDataFolder);
        this.CommandDictionary = commandDictionary;
        this.ColorDictionary = colorDictionary;
        this.TwitchClient = twitchClient;
        this.TwitchAPI = twitchAPI;
    }

    protected string ModuleDataFolder { get; private set; }

    protected CommandDictionary CommandDictionary { get; private set; }

    protected ColorDictionary ColorDictionary { get; private set; }

    protected TwitchClient TwitchClient { get; private set; }

    protected TwitchAPI TwitchAPI { get; private set; }
}
