namespace Goofbot.Utils;

using System;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class Command
{
    private readonly string name;
    private readonly object module;
    private readonly Func<string, OnChatCommandReceivedArgs, bool, Task<string>> commandAction;
    private readonly TimeSpan timeout;
    private readonly CommandAccessibilityModifier commandAccessibilityModifier;

    private DateTime timeOfLastInvocation = DateTime.MinValue;

    public Command(string name, Func<string, OnChatCommandReceivedArgs, bool, Task<string>> commandAction, int timeoutSeconds, CommandAccessibilityModifier commandAccessibilityModifier = CommandAccessibilityModifier.AllChatters)
    {
        this.name = name;
        this.timeout = TimeSpan.FromSeconds(timeoutSeconds);
        this.commandAccessibilityModifier = commandAccessibilityModifier;
        this.commandAction = commandAction;
    }

    public string Name
    {
        get { return this.name; }
    }

    public async Task<string> ExecuteCommandAsync(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        if (this.commandAccessibilityModifier == CommandAccessibilityModifier.StreamerOnly && !eventArgs.Command.ChatMessage.IsBroadcaster)
        {
            // return "I don't answer to you, peasant! OhMyDog (this command is for Goof's use only)";
            return string.Empty;
        }
        else if (this.commandAccessibilityModifier == CommandAccessibilityModifier.SubOnly && !eventArgs.Command.ChatMessage.IsSubscriber)
        {
            return string.Empty;
        }
        else if (this.commandAccessibilityModifier == CommandAccessibilityModifier.ModOnly && !eventArgs.Command.ChatMessage.IsModerator)
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
