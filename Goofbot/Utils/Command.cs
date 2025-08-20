using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace Goofbot.Utils
{
    internal class Command
    {
        private readonly string _name;
        private readonly object _module;
        private readonly Func<object, string, string> _commandAction;
        private readonly TimeSpan _timeout;
        private readonly bool _goofOnly;

        private DateTime _timeOfLastInvocation = DateTime.MinValue;

        public string Name { get { return _name; } }

        public Command(string name, object module, Func<object, string, string> commandAction, int timeoutSeconds, bool goofOnly = false)
        {
            _name = name;
            _timeout = TimeSpan.FromSeconds(timeoutSeconds);
            _goofOnly = goofOnly;
            _module = module;
            _commandAction = commandAction;
        }

        public string ExecuteCommand(string commandArgs, OnMessageReceivedArgs messageArgs)
        {
            if (_goofOnly && !messageArgs.ChatMessage.IsBroadcaster)
            {
                return "I don't answer to you, peasant! OhMyDog (this command is for Goof's use only)";
            }

            DateTime invocationTime = DateTime.UtcNow;
            DateTime timeoutTime = _timeOfLastInvocation.Add(_timeout);
            if (timeoutTime.CompareTo(invocationTime) < 0)
            {
                _timeOfLastInvocation = invocationTime;
                return _commandAction(_module, commandArgs);
            }
            else
            {
                return "";
            }
        }
    }
}
