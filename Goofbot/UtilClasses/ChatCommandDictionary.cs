namespace Goofbot.UtilClasses;

using System.Collections;
using System.Collections.Generic;

internal class ChatCommandDictionary : IEnumerable<KeyValuePair<string, ChatCommand>>
{
    private readonly Dictionary<string, ChatCommand> commandDictionary = [];

    public ChatCommandDictionary()
    {
    }

    public bool TryAddCommand(ChatCommand command)
    {
        return this.commandDictionary.TryAdd(command.Name, command);
    }

    public bool TryGetCommand(string name, out ChatCommand command)
    {
        return this.commandDictionary.TryGetValue(name, out command);
    }

    public IEnumerator<KeyValuePair<string, ChatCommand>> GetEnumerator()
    {
        return this.commandDictionary.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}
