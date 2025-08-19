using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace Goofbot
{
    internal class Command
    {
        public static event EventHandler NotBroadcaster;

        private readonly TimeSpan _timeout;
        public readonly bool goofOnly;
        private DateTime _timeOfLastInvocation = DateTime.MinValue;
        public event EventHandler<string> ExecuteCommand;

        public Command(int timeoutSeconds, bool goofOnly = false)
        {
            _timeout = TimeSpan.FromSeconds(timeoutSeconds);
            this.goofOnly = goofOnly;
        }

        public void IssueCommand(string commandArgs, OnMessageReceivedArgs messageArgs)
        {
            if (goofOnly && !messageArgs.ChatMessage.IsBroadcaster)
            {
                OnNotBroadcaster();
                return;
            }

            DateTime invocationTime = DateTime.UtcNow;
            DateTime timeoutTime = _timeOfLastInvocation.Add(_timeout);
            if (timeoutTime.CompareTo(invocationTime) < 0)
            {
                _timeOfLastInvocation = invocationTime;
                OnExecuteCommand(commandArgs);
            }
            /*                else
                            {
                                TimeSpan timeUntilTimeoutElapses = timeoutTime.Subtract(invocationTime);
                                OnTimeoutNotElapsed(timeUntilTimeoutElapses);
                            }*/
        }

        protected virtual void OnExecuteCommand(string commandArgs)
        {
            ExecuteCommand?.Invoke(this, commandArgs);
        }

        /*protected virtual void OnTimeoutNotElapsed(TimeSpan timeUntilTimeoutElapses)
        {
            TimeoutNotElapsed?.Invoke(this, timeUntilTimeoutElapses);
        }*/

        protected virtual void OnNotBroadcaster()
        {
            NotBroadcaster?.Invoke(this, new EventArgs());
        }
    }
}
