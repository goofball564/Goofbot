namespace Goofbot;

using Goofbot.Modules;
using Goofbot.Utils;
using ImageMagick;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets;

[SupportedOSPlatform("windows")]
internal class Program
{
    public const string TwitchBotUsername = "goofbotthebot";
    public const string TwitchChannelUsername = "goofballthecat";

    private static readonly string GoofbotAppDataFolder = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Goofbot");

    public static TwitchAuthenticationManager TwitchAuthenticationManager { get; private set; }

    public static ColorDictionary ColorDictionary { get; private set; }

    public static TwitchAPI TwitchAPI { get; private set; } = new ();

    public static TwitchClient TwitchClient { get; private set; } = new ();

    public static string StuffFolder { get; private set; }

    public static CommandDictionary CommandDictionary { get; private set; } = new ();

    public static EventSubWebsocketClient EventSubWebsocketClient { get; private set; }

    public static async Task Main(string[] args)
    {
        // Get location of bot data folder
        string stuffLocationFile = Path.Join(GoofbotAppDataFolder, "stufflocation.txt");
        StuffFolder = File.ReadAllText(stuffLocationFile).Trim();

        // Create color dictionary
        string colorNamesFile = Path.Join(StuffFolder, "color_names.json");
        ColorDictionary = new (colorNamesFile);
        Task colorDictionaryTask = Task.Run(async () => { await ColorDictionary.Initialize(); });

        // Initialize TwitchClient and TwitchAPI, authenticate with twitch
        string twitchAppCredentialsFile = Path.Combine(StuffFolder, "twitch_credentials.json");
        dynamic twitchAppCredentials = ParseJsonFile(twitchAppCredentialsFile);
        string clientID = twitchAppCredentials.client_id;
        string clientSecret = twitchAppCredentials.client_secret;
        TwitchAuthenticationManager = new (clientID, clientSecret, TwitchClient, TwitchAPI);
        Task authenticationManagerInitializeTask = TwitchAuthenticationManager.Initialize();

        // Initialize Magick.NET
        MagickNET.Initialize();

        await authenticationManagerInitializeTask;

        // Subscribe to TwitchClient events
        TwitchClient.OnLog += Client_OnLog;
        TwitchClient.OnConnected += Client_OnConnected;
        TwitchClient.OnIncorrectLogin += Client_OnIncorrectLogin;
        TwitchClient.OnChatCommandReceived += Client_OnChatCommandReceived;
        TwitchClient.AddChatCommandIdentifier('!');

        // Subscribe to Twitch EventSub for Channel Point Redemption
        ChannelPointRedemptionEventSub channelPointRedemptionEventSub = new ();
        EventSubWebsocketClient = channelPointRedemptionEventSub.EventSubWebsocketClient;

        // Initialize Modules
        SpotifyModule spotifyModule = new ("SpotifyModule");
        Task spotifyModuleInitializeTask = spotifyModule.Initialize();

        SoundAlertModule soundAlertModule = new ("SoundAlertModule");
        MiscCommandsModule miscCommandsModule = new ("MiscCommandsModule");
        CalculatorModule calculatorModule = new ("CalculatorModule");
        EmoteSoundModule emoteSoundModule = new ("EmoteSoundModule");
        BlueGuyModule blueGuyModule = new ("BlueGuyModule");
        TextToSpeechModule textToSpeechModule = new ("TextToSpeechModule");

        await spotifyModuleInitializeTask;
        await colorDictionaryTask;

        // Start the bot
        blueGuyModule.StartTimer();
        TwitchClient.Connect();
        while (true)
        {
            Console.ReadLine();
        }
    }

    public static dynamic ParseJsonFile(string filename)
    {
        string jsonString = File.ReadAllText(filename);
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

    private static void Client_OnLog(object sender, OnLogArgs e)
    {
        Console.WriteLine($"{e.DateTime}: {e.BotUsername} - {e.Data}");
    }

    private static void Client_OnConnected(object sender, OnConnectedArgs e)
    {
        Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        TwitchClient.SendMessage(TwitchChannelUsername, "Goofbot is activated and at your service MrDestructoid");
    }

    private static async void Client_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
    {
        string message = string.Empty;
        string commandName = e.Command.CommandText.ToLowerInvariant().Trim();
        string commandArgs = e.Command.ArgumentsAsString.Trim();

        bool isReversed = false;

        Command command;
        if (CommandDictionary.TryGetCommand(commandName, out command))
        {
            message = await command.ExecuteCommandAsync(commandArgs, e, isReversed);
        }
        else if (CommandDictionary.TryGetCommand(ReverseString(commandName), out command))
        {
            isReversed = true;

            string commandArgsReversed = Program.ReverseString(commandArgs);

            message = await command.ExecuteCommandAsync(commandArgsReversed, e, isReversed);
            message = ReverseString(message);
        }

        if (!message.Equals(string.Empty))
        {
            TwitchClient.SendMessage(TwitchChannelUsername, message);
        }
    }

    private static void Client_OnIncorrectLogin(object sender, OnIncorrectLoginArgs e)
    {
    }
}
