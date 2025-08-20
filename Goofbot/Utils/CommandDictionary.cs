namespace Goofbot.Utils;

using System.Collections.Generic;

internal class CommandDictionary
{
    private readonly Dictionary<string, Command> commandDictionary = new ();

    public CommandDictionary()
    {

    }

    public bool TryAddCommand(Command command)
    {
        return this.commandDictionary.TryAdd(command.Name, command);
    }

    public bool TryGetCommand(string name, out Command command)
    {
        return this.commandDictionary.TryGetValue(name, out command);
    }
}
