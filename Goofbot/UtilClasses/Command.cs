namespace Goofbot.UtilClasses;

using System;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class Command
{
    public readonly CommandAccessibilityModifier CommandAccessibilityModifier;
    public readonly string Name;
    public readonly bool Unlisted;

    private readonly Func<string, bool, OnChatCommandReceivedArgs, Task> commandAction;
    private readonly TimeSpan timeout;

    private DateTime timeOfLastInvocation = DateTime.MinValue;

    public Command(string name, Func<string, bool, OnChatCommandReceivedArgs, Task> commandAction, CommandAccessibilityModifier commandAccessibilityModifier = CommandAccessibilityModifier.AllChatters, bool unlisted = false, int timeoutSeconds = 1)
    {
        this.Name = name;
        this.CommandAccessibilityModifier = commandAccessibilityModifier;
        this.commandAction = commandAction;
        this.Unlisted = unlisted;

        if (this.CommandAccessibilityModifier == CommandAccessibilityModifier.StreamerOnly)
        {
            this.timeout = TimeSpan.FromSeconds(0);
        }
        else
        {
            this.timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }
    }

    public async Task ExecuteCommandAsync(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        if (this.CommandAccessibilityModifier == CommandAccessibilityModifier.StreamerOnly && !eventArgs.Command.ChatMessage.IsBroadcaster)
        {
            return;
        }
        else if (this.CommandAccessibilityModifier == CommandAccessibilityModifier.SubOnly && !eventArgs.Command.ChatMessage.IsSubscriber)
        {
            return;
        }
        else if (this.CommandAccessibilityModifier == CommandAccessibilityModifier.ModOnly && !eventArgs.Command.ChatMessage.IsModerator)
        {
            return;
        }

        DateTime invocationTime = DateTime.UtcNow;
        DateTime timeoutTime = this.timeOfLastInvocation.Add(this.timeout);
        if (timeoutTime.CompareTo(invocationTime) < 0)
        {
            this.timeOfLastInvocation = invocationTime;
            await this.commandAction(commandArgs, isReversed, eventArgs);
        }
    }
}

public enum CommandAccessibilityModifier
{
    StreamerOnly,
    SubOnly,
    ModOnly,
    AllChatters,
}
