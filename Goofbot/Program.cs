namespace Goofbot;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

internal class Program
{
    private const string TwitchBotUsername = "goofbotthebot";
    private const string TwitchChannelUsername = "goofballthecat";

    public static async Task Main()
    {
        CancellationTokenSource cancellationTokenSource = new ();
        using Bot bot = new (TwitchBotUsername, TwitchChannelUsername, cancellationTokenSource);
        await bot.StartAsync();

        // Let the bot do its thing
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
        }
        catch
        {
        }
    }

    public static dynamic ParseJsonFile(string file)
    {
        string jsonString = File.ReadAllText(file);
        return JsonConvert.DeserializeObject(jsonString);
    }

    public static async Task<dynamic> ParseJsonFileAsync(string file)
    {
        string jsonString = await File.ReadAllTextAsync(file);
        return JsonConvert.DeserializeObject(jsonString);
    }

    public static string ReverseString(string str)
    {
        char[] charArray = str.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }

    public static string RemoveSpaces(string str)
    {
        return str.Replace(" ", string.Empty);
    }
}
