namespace Goofbot.UtilClasses;

using Goofbot.UtilClasses.Enums;
using TwitchLib.Client.Events;

internal class BlackjackCommand(BlackjackCommandType command, string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
{
    public readonly BlackjackCommandType CommandType = command;
    public readonly string UserID = eventArgs.Command.ChatMessage.UserId;
    public readonly string UserName = eventArgs.Command.ChatMessage.DisplayName;
    public readonly string CommandArgs = commandArgs;
    public readonly bool IsReversed = isReversed;
    public readonly OnChatCommandReceivedArgs EventArgs = eventArgs;
}
