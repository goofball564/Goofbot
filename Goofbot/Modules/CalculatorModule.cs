namespace Goofbot.Modules;

using AngouriMath;
using Goofbot.Utils;
using System;
using System.Text;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;

internal class CalculatorModule : GoofbotModule
{
    public CalculatorModule(string moduleDataFolder)
        : base(moduleDataFolder)
    {
        Program.TwitchClient.OnMessageReceived += this.Client_OnMessageReceived;
    }

    private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
    {
        string botMessage = string.Empty;
        string chatMessage = e.ChatMessage.Message.Trim();
        try
        {
            Entity expr = chatMessage;
            if (expr.EvaluableNumerical)
            {
                double result = (double)expr.EvalNumerical();
                botMessage = string.Format("{0:0.#######}", result);
            }
        }
        catch
        {
        }

        if (!(botMessage.Equals(string.Empty) || botMessage.Equals(chatMessage)))
        {
            Program.TwitchClient.SendMessage(Program.TwitchChannelUsername, botMessage);
        }
    }
}
