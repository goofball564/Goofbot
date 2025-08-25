namespace Goofbot;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AngouriMath;
using Goofbot.Modules;
using Goofbot.Utils;
using ImageMagick;
using Newtonsoft.Json;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;

internal class Program
{
    public const string TwitchBotUsername = "goofbotthebot";
    public const string TwitchChannelUsername = "goofballthecat";

    private static readonly CommandDictionary CommandDictionary = new ();
    private static readonly string GoofbotAppDataFolder = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Goofbot");

    public static TwitchAuthenticationManager TwitchAuthenticationManager { get; private set; }

    public static ColorDictionary ColorDictionary { get; private set; }

    public static TwitchAPI TwitchAPI { get; private set; } = new ();

    public static TwitchClient TwitchClient { get; private set; } = new ();

    public static string StuffFolder { get; private set; }

    public static async Task Main(string[] args)
    {
        // Get location of bot data folder
        string stuffLocationFile = Path.Join(GoofbotAppDataFolder, "stufflocation.txt");
        StuffFolder = File.ReadAllText(stuffLocationFile).Trim();

        // Create color dictionary
        string colorNamesFile = Path.Join(StuffFolder, "color_names.json");
        ColorDictionary = new (colorNamesFile);
        Task colorDictionaryTask = Task.Run(async () => { await ColorDictionary.Initialize(); });

        // initialize TwitchClient and TwitchAPI, authenticate with twitch
        string twitchAppCredentialsFile = Path.Combine(StuffFolder, "twitch_credentials.json");
        dynamic twitchAppCredentials = ParseJsonFile(twitchAppCredentialsFile);
        string clientID = twitchAppCredentials.client_id;
        string clientSecret = twitchAppCredentials.client_secret;
        TwitchAuthenticationManager = new (clientID, clientSecret, TwitchClient, TwitchAPI);
        Task authenticationManagerInitializeTask = TwitchAuthenticationManager.Initialize();

        SpotifyModule spotifyModule = new ("SpotifyModule", CommandDictionary);
        Task spotifyModuleInitializeTask = spotifyModule.Initialize();

        MagickNET.Initialize();

        TwitchClient.OnLog += Client_OnLog;
        TwitchClient.OnConnected += Client_OnConnected;
        TwitchClient.OnIncorrectLogin += Client_OnIncorrectLogin;
        TwitchClient.OnChatCommandReceived += Client_OnChatCommandReceived;

        SoundAlertModule soundAlertModule = new ();
        MiscCommandsModule miscCommandsModule = new ("MiscCommandsModule", CommandDictionary);
        CalculatorModule calculatorModule = new (TwitchClient);
        EmoteSoundModule emoteSoundModule = new ("EmoteSoundModule", TwitchClient);

        await authenticationManagerInitializeTask;
        await spotifyModuleInitializeTask;
        await colorDictionaryTask;
        BlueGuyModule blueGuyModule = new ("BlueGuyModule", CommandDictionary, TwitchClient);
        TwitchClient.AddChatCommandIdentifier('!');
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
        string commandName = e.Command.CommandText;
        string commandArgs = e.Command.ArgumentsAsString;

        bool isReversed = false;

        Command command;
        if (CommandDictionary.TryGetCommand(commandName, out command))
        {
            message = await command.ExecuteCommandAsync(commandArgs, e, isReversed);
        }
        else if (CommandDictionary.TryGetCommand(ReverseString(commandName), out command))
        {
            isReversed = true;

            List<string> a = e.Command.ArgumentsAsList;
            for (int i = 0; i < a.Count; i++)
            {
                a[i] = ReverseString(a[i]);
            }

            string commandArgsReversed = string.Join(" ", a);

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
