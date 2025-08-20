using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Goofbot.Utils
{
    internal class CommandDictionary
    {
        private readonly Dictionary<string, Command> _commandDictionary = new();

        public CommandDictionary()
        {

        }

        public bool TryAddCommand(Command command)
        {
            return _commandDictionary.TryAdd(command.Name, command);
        }

        public bool TryGetCommand(string name, out Command command)
        {
            return _commandDictionary.TryGetValue(name, out command);
        }
    }
}
