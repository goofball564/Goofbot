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
            Entity expr = e.ChatMessage.Message;

            if (expr.EvaluableNumerical)
            {
                Entity eval = expr.Evaled;
                string evalString = eval.ToString();
                if (!Program.RemoveSpaces(expr.ToString()).Equals(eval.ToString()))
                {
                    if (eval is not Entity.Number.Rational)
                    {
                        message = string.Format("{0:F7}", (double)(Entity.Number)eval);
                    }
                    else
                    {
                        message = eval.ToString();
                    }
                }
                else if (evalString.Contains('/'))
                {
                    string[] nums = evalString.Split("/");
                    if (nums.Length == 2)
                    {
                        double result = double.Parse(nums[0]) / double.Parse(nums[1]);
                        message = string.Format("{0:0.#######}", result);
                    }
                    else
                    {
                        message = "Goof, fix your damn calculator.";
                    }
                }
            }
        }
        catch
        {
        }
        finally
        {
            if (!message.Equals(string.Empty))
            {
                this.twitchClient.SendMessage(Program.TwitchChannelUsername, message);
            }
        }
    }
}
