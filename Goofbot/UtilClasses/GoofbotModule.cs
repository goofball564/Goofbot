namespace Goofbot.Utils;

using System;
using System.IO;

internal abstract class GoofbotModule : IDisposable
{
    protected readonly string moduleDataFolder;

    protected GoofbotModule(string moduleDataFolder)
    {
        this.moduleDataFolder = Path.Join(Program.StuffFolder, moduleDataFolder);

    }

    public virtual void Dispose()
    {

    }
}
