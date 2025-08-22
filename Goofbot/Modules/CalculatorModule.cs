namespace Goofbot.Modules;

using AngouriMath;
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
        string message = string.Empty;
        try
        {
            using (var _ = MathS.Settings.FloatToRationalIterCount.Set(0))
            {
                Entity expr = e.ChatMessage.Message;

                if (expr.EvaluableNumerical)
                {
                    Entity eval = expr.Evaled;
                    string evalString = eval.ToString();
                    message = string.Format("{0:0.#######}", (double)(Entity.Number)eval);
                }
            }
        }
        catch
        {
        }

        if (!message.Equals(string.Empty))
        {
            this.twitchClient.SendMessage(Program.TwitchChannelUsername, message);
        }
    }
}
