using System;
using TwitchLib.Client.Events;

namespace Goofbot.Modules
{
    internal class CommandParsingModule
    {
        public Command BlueGuyCommand = new Command(1);
        public Command SongCommand = new Command(1);
        public Command FarmModeCommand = new Command(0, true);
        public Command QueueModeCommand = new Command(0, true);
        public Command RefreshColorsCommand = new Command(0, true);

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
                case "!refreshcolors":
                    RefreshColorsCommand.IssueCommand(commandArgs, messageArgs);
                    break;
                default:
                    break;
            }
        }
    }
}
