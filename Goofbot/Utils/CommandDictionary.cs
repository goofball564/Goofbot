namespace Goofbot.Utils;

using System.Collections.Generic;
using System.Windows.Forms.VisualStyles;

internal class CommandDictionary
{
    private readonly Dictionary<string, Command> commandDictionary = [];

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

    public List<string> GetAllCommands()
    {
        List<string> commands = [];
        foreach (string command in this.commandDictionary.Keys)
        {
            commands.Add(command);
        }

        return commands;
    }
}
