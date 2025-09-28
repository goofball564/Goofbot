namespace Goofbot.UtilClasses;

using System;
using System.IO;
using System.Threading.Tasks;

internal abstract class GoofbotModule : IDisposable
{
    protected readonly string moduleDataFolder;
    protected readonly Bot bot;

    protected GoofbotModule(Bot bot, string moduleDataFolder)
    {
        this.bot = bot;

        this.moduleDataFolder = Path.Join(this.bot.StuffFolder, moduleDataFolder);
        Directory.CreateDirectory(this.moduleDataFolder);
    }

    public virtual void Dispose()
    {
    }

    public virtual async Task InitializeAsync()
    {
    }
}
