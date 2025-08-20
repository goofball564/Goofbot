namespace Goofbot.Utils;

using System.IO;

internal abstract class GoofbotModule
{
    private readonly string moduleDataFolder;

    protected GoofbotModule(string moduleDataFolder)
    {
        this.moduleDataFolder = Path.Combine(Program.StuffFolder, moduleDataFolder);
    }
}
