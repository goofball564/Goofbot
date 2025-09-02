namespace Goofbot.Utils;

using System;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class Command
{
    public readonly CommandAccessibilityModifier CommandAccessibilityModifier;
    public readonly string Name;
    public readonly bool Unlisted;

    private readonly Func<string, OnChatCommandReceivedArgs, bool, Task<string>> commandAction;
    private readonly TimeSpan timeout;

    private DateTime timeOfLastInvocation = DateTime.MinValue;

    public Command(string name, Func<string, OnChatCommandReceivedArgs, bool, Task<string>> commandAction, CommandAccessibilityModifier commandAccessibilityModifier = CommandAccessibilityModifier.AllChatters, bool unlisted = false, int timeoutSeconds = 1)
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

    public async Task<string> ExecuteCommandAsync(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        if (this.CommandAccessibilityModifier == CommandAccessibilityModifier.StreamerOnly && !eventArgs.Command.ChatMessage.IsBroadcaster)
        {
            // return "I don't answer to you, peasant! OhMyDog (this command is for Goof's use only)";
            return string.Empty;
        }
        else if (this.CommandAccessibilityModifier == CommandAccessibilityModifier.SubOnly && !eventArgs.Command.ChatMessage.IsSubscriber)
        {
            return string.Empty;
        }
        else if (this.CommandAccessibilityModifier == CommandAccessibilityModifier.ModOnly && !eventArgs.Command.ChatMessage.IsModerator)
        {
            return string.Empty;
        }

        DateTime invocationTime = DateTime.UtcNow;
        DateTime timeoutTime = this.timeOfLastInvocation.Add(this.timeout);
        if (timeoutTime.CompareTo(invocationTime) < 0)
        {
            this.timeOfLastInvocation = invocationTime;
            return await this.commandAction(commandArgs, eventArgs, isReversed);
        }
        else
        {
            return string.Empty;
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
