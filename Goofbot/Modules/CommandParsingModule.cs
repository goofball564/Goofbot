using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace Goofbot.Modules
{
    internal class CommandParsingModule
    {
        // time of last invocation or time since last invocation
        private DateTime timeOfLastInvocation = DateTime.MinValue;
        private TimeSpan timeout = TimeSpan.FromSeconds(0); 
        public event EventHandler<string> GuyCommand;

        public event EventHandler<TimeSpan> TimeoutNotElapsed;

        public CommandParsingModule()
        {

        }

        protected virtual void OnGuyCommand(string args)
        {
            GuyCommand?.Invoke(this, args);
        }

        protected virtual void OnTimeoutNotElapsed(TimeSpan timeUntilTimeoutElapses)
        {
            TimeoutNotElapsed?.Invoke(this, timeUntilTimeoutElapses);
        }

        public void ParseMessageForCommand(OnMessageReceivedArgs e)
        {
            DateTime invocationTime = DateTime.UtcNow;
            string trimmedMessage = e.ChatMessage.Message.Trim();
            int indexOfSpace = trimmedMessage.IndexOf(' ');

            string command;
            string args;

            if (indexOfSpace != -1)
            {
                command = trimmedMessage.Substring(0, indexOfSpace);
                args = trimmedMessage.Substring(indexOfSpace + 1);
            }
            else
            {
                command = trimmedMessage;
                args = "";
            }

            command = command.ToLowerInvariant();
            args = args.Trim();
            switch (command)
            {
                case "!guy":
                    DateTime timeoutTime = timeOfLastInvocation.Add(timeout);
                    if (timeoutTime.CompareTo(invocationTime) < 0)
                    {
                        timeOfLastInvocation = invocationTime;
                        OnGuyCommand(args);
                    }
                    else
                    {
                        TimeSpan timeUntilTimeoutElapses = timeoutTime.Subtract(invocationTime);
                        OnTimeoutNotElapsed(timeUntilTimeoutElapses);
                    }
                    
                    break;
                default:
                    break;
            }
        }
    }
}
