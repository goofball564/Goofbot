namespace Goofbot.Utils;

using System.Collections;
using System.Collections.Generic;

internal class CommandDictionary : IEnumerable<KeyValuePair<string, Command>>
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

    public IEnumerator<KeyValuePair<string, Command>> GetEnumerator()
    {
        return this.commandDictionary.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
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
