namespace Goofbot.UtilClasses;

using Goofbot.UtilClasses.Enums;
using TwitchLib.Client.Events;

// string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs
internal class BlackjackCommand(BlackjackCommandType command, string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
{
    public readonly BlackjackCommandType CommandType = command;
    public readonly string UserID = eventArgs.Command.ChatMessage.UserId;
    public readonly string Args = commandArgs;
    public readonly bool IsReversed = isReversed;
    public readonly OnChatCommandReceivedArgs EventArgs = eventArgs;
}
