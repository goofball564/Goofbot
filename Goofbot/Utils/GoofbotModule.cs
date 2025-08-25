namespace Goofbot.Utils;

using System.IO;

internal abstract class GoofbotModule
{
    protected GoofbotModule(string moduleDataFolder)
    {
        this.ModuleDataFolder = Path.Combine(Program.StuffFolder, moduleDataFolder);
    }

    protected string ModuleDataFolder { get; private set; }
}
