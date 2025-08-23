namespace Goofbot.Modules;

using AngouriMath;
using System;
using System.Text;
using TwitchLib.Client;
using TwitchLib.Client.Events;

internal class CalculatorModule
{
    private readonly TwitchClient twitchClient;

    public CalculatorModule(TwitchClient twitchClient)
    {
        this.twitchClient = twitchClient;
        this.twitchClient.OnMessageReceived += this.Client_OnMessageReceived;
    }

    private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
    {
        StringBuilder botMessage = new ();
        string chatMessage = Program.RemoveSpaces(e.ChatMessage.Message.Trim());
        try
        {
            Entity expr = chatMessage;
            if (expr.EvaluableNumerical)
            {
                var eval = expr.Evaled;
                botMessage.Append(string.Format("{0:0.#######}", (double)(Entity.Number)eval));
            }
        }
        catch
        {
        }

        if (!(botMessage.Equals(string.Empty) || botMessage.Equals(chatMessage)))
        {
            this.twitchClient.SendMessage(Program.TwitchChannelUsername, botMessage.ToString());
        }
    }
}
