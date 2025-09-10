namespace Goofbot.Modules;

using AngouriMath;
using Goofbot.Utils;
using System.Threading;
using TwitchLib.Client.Events;

internal class CalculatorModule : GoofbotModule
{
    public CalculatorModule(Bot bot, string moduleDataFolder, CancellationToken cancellationToken)
        : base(bot, moduleDataFolder, cancellationToken)
    {
        this.bot.TwitchClient.OnMessageReceived += this.Client_OnMessageReceived;
        cancellationToken.Register(this.OnCancellation);
    }

    private void OnCancellation()
    {
        this.bot.TwitchClient.OnMessageReceived -= this.Client_OnMessageReceived;
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
            this.bot.TwitchClient.SendMessage(this.bot.TwitchChannelUsername, botMessage);
        }
    }
}
