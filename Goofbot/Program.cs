namespace Goofbot;

using Goofbot.Modules;
using Goofbot.Utils;
using ImageMagick;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets;

/*
 * Some ideas I've had/things to do
 *
 * EASY
 * Ability to select Mike/Mary/Sam for TTS
 * !yug inverts BlueGuy image
 *
 * MODIFY EXISTING MODULES
 * !stt reverses audio stream
 * DONE---Ability to cancel specific users for TTS
 * Ability to block specific users for TTS
 * Add imaginary number support to calculator
 * Ability to unlock and re-lock sub-only commands
 *
 * NEW
 * SCATTER for specific randomly chosen users
 * Timers
 * Sentiment analysis to react to angry messages
 * Implement configurable settings
 * Interface to manage pure text commands, and migrate from Nightbot
 * Web page that shows up-to-date commands (requires a web page)
 * Add ability for users to send voice messages (requires a web page)
 *
 */

internal class Program
{
    private const string TwitchBotUsername = "goofbotthebot";
    private const string TwitchChannelUsername = "goofballthecat";

    public static async Task Main()
    {
        Bot bot = new (TwitchBotUsername, TwitchChannelUsername);
        await bot.InitializeAsync();
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
