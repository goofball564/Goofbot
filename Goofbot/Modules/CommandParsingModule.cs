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
        public Command BlueGuyCommand = new Command(0);
        public Command SongCommand = new Command(1);
        public Command FarmModeCommand = new Command(0, true);
        public Command QueueModeCommand = new Command(0, true);

        public static event EventHandler<TimeSpan> TimeoutNotElapsed;
        public static event EventHandler NotBroadcaster;

        public CommandParsingModule()
        {

        }

        public void ParseMessageForCommand(OnMessageReceivedArgs messageArgs)
        {
            DateTime invocationTime = DateTime.UtcNow;
            string trimmedMessage = messageArgs.ChatMessage.Message.Trim();
            int indexOfSpace = trimmedMessage.IndexOf(' ');

            string command;
            string commandArgs;

            if (indexOfSpace != -1)
            {
                command = trimmedMessage.Substring(0, indexOfSpace);
                commandArgs = trimmedMessage.Substring(indexOfSpace + 1);
            }
            else
            {
                command = trimmedMessage;
                commandArgs = "";
            }

            command = command.ToLowerInvariant();
            commandArgs = commandArgs.Trim();
            switch (command)
            {
                case "!guy":
                    BlueGuyCommand.IssueCommand(commandArgs, messageArgs);
                    break;
                case "!song":
                    SongCommand.IssueCommand(commandArgs, messageArgs);
                    break;
                case "!farmmode":
                    break;
                case "!queuemode":
                    QueueModeCommand.IssueCommand(commandArgs, messageArgs);
                    break;
                default:
                    break;
            }
        }

        public class Command
        {
            private DateTime timeOfLastInvocation = DateTime.MinValue;
            private TimeSpan timeout;
            public readonly bool GoofOnly;
            public event EventHandler<string> ExecuteCommand;

            public Command(int timeoutSeconds, bool goofOnly = false)
            {
                timeout = TimeSpan.FromSeconds(timeoutSeconds);
                GoofOnly = goofOnly;
            }

            public void IssueCommand(string commandArgs, OnMessageReceivedArgs messageArgs)
            {
                if (GoofOnly && !messageArgs.ChatMessage.IsBroadcaster)
                {
                    OnNotBroadcaster();
                    return;
                }

                DateTime invocationTime = DateTime.UtcNow;
                DateTime timeoutTime = timeOfLastInvocation.Add(timeout);
                if (timeoutTime.CompareTo(invocationTime) < 0)
                {
                    timeOfLastInvocation = invocationTime;
                    OnExecuteCommand(commandArgs);
                }
                else
                {
                    TimeSpan timeUntilTimeoutElapses = timeoutTime.Subtract(invocationTime);
                    OnTimeoutNotElapsed(timeUntilTimeoutElapses);
                }
            }

            protected virtual void OnExecuteCommand(string commandArgs)
            {
                ExecuteCommand?.Invoke(this, commandArgs);
            }

            protected virtual void OnTimeoutNotElapsed(TimeSpan timeUntilTimeoutElapses)
            {
                TimeoutNotElapsed?.Invoke(this, timeUntilTimeoutElapses);
            }

            protected virtual void OnNotBroadcaster()
            {
                NotBroadcaster?.Invoke(this, new EventArgs());
            }


        }
    }
}
