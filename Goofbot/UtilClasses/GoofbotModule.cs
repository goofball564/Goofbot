namespace Goofbot.Utils;

using System.IO;

internal abstract class GoofbotModule
{
    protected readonly string moduleDataFolder;

    protected GoofbotModule(string moduleDataFolder)
    {
        this.moduleDataFolder = Path.Join(Program.StuffFolder, moduleDataFolder);

    }

}
