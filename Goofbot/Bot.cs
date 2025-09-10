namespace Goofbot;

using Goofbot.Modules;
using Goofbot.Utils;
using ImageMagick;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets;

internal class Bot : IDisposable
{
    public readonly string TwitchBotUsername;
    public readonly string TwitchChannelUsername;

    public readonly TwitchAPI TwitchAPI;
    public readonly TwitchClient TwitchClient;
    public readonly CommandDictionary CommandDictionary;

    private readonly CancellationTokenSource cancellationTokenSource;

    private readonly string goofbotAppDataFolder = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Goofbot");

    private readonly SpotifyModule spotifyModule;
    private readonly SoundAlertModule soundAlertModule;
    private readonly MiscCommandsModule miscCommandsModule;
    private readonly CalculatorModule calculatorModule;
    private readonly EmoteSoundModule emoteSoundModule;
    private readonly BlueGuyModule blueGuyModule;
    private readonly TextToSpeechModule textToSpeechModule;

    public Bot(string twitchBotUsername, string twitchChannelUsername)
    {
        this.TwitchBotUsername = twitchBotUsername;
        this.TwitchChannelUsername = twitchChannelUsername;

        this.TwitchAPI = new ();
        this.TwitchClient = new ();
        this.CommandDictionary = new ();
        this.cancellationTokenSource = new ();

        // Get location of bot data folder
        string stuffLocationFile = Path.Join(this.goofbotAppDataFolder, "stufflocation.txt");
        this.StuffFolder = File.ReadAllText(stuffLocationFile).Trim();

        // Create color dictionary
        string colorNamesFile = Path.Join(this.StuffFolder, "color_names.json");
        this.ColorDictionary = new (colorNamesFile);

        // Create Twitch Authentication Manager with client ID and client secret
        string twitchAppCredentialsFile = Path.Join(this.StuffFolder, "twitch_credentials.json");
        dynamic twitchAppCredentials = Program.ParseJsonFile(twitchAppCredentialsFile);
        string clientID = twitchAppCredentials.client_id;
        string clientSecret = twitchAppCredentials.client_secret;
        this.TwitchAuthenticationManager = new (this, clientID, clientSecret);

        // Subscribe to TwitchClient events
        this.TwitchClient.OnLog += this.Client_OnLog;
        this.TwitchClient.OnConnected += this.Client_OnConnected;
        this.TwitchClient.OnIncorrectLogin += this.Client_OnIncorrectLogin;
        this.TwitchClient.OnChatCommandReceived += this.Client_OnChatCommandReceived;

        // Initialize Magick.NET
        MagickNET.Initialize();

        // Instantiate modules
        this.spotifyModule = new (this, "SpotifyModule", this.cancellationTokenSource.Token);
        this.soundAlertModule = new (this, "SoundAlertModule", this.cancellationTokenSource.Token);
        this.miscCommandsModule = new (this, "MiscCommandsModule", this.cancellationTokenSource.Token);
        this.calculatorModule = new (this, "CalculatorModule", this.cancellationTokenSource.Token);
        this.emoteSoundModule = new (this, "EmoteSoundModule", this.cancellationTokenSource.Token);
        this.blueGuyModule = new (this, "BlueGuyModule", this.cancellationTokenSource.Token);
        this.textToSpeechModule = new (this, "TextToSpeechModule", this.cancellationTokenSource.Token);
    }

    public TwitchAuthenticationManager TwitchAuthenticationManager { get; private set; }

    public ColorDictionary ColorDictionary { get; private set; }

    public string StuffFolder { get; private set; }

    public EventSubWebsocketClient EventSubWebsocketClient { get; private set; }

    public async Task Start()
    {
        Task colorDictionaryTask = this.ColorDictionary.InitializeAsync();
        Task authenticationManagerInitializeTask = this.TwitchAuthenticationManager.InitializeAsync();
        Task spotifyModuleInitializeTask = this.spotifyModule.InitializeAsync();

        // Twitch API Authentication Required for EventSub
        await authenticationManagerInitializeTask;

        // Subscribe to Twitch EventSub for Channel Point Redemption
        // Requires TwitchAPI to be initialized
        ChannelPointRedemptionEventSub channelPointRedemptionEventSub = new (this);
        this.EventSubWebsocketClient = channelPointRedemptionEventSub.EventSubWebsocketClient;

        // Requires EventSubWebsocketClient to be initialized
        this.soundAlertModule.Initialize();
        this.textToSpeechModule.Initialize();

        // Requires TwitchClient to be initialized
        this.TwitchClient.AddChatCommandIdentifier('!');

        // Finish everything else before starting the bot
        await spotifyModuleInitializeTask;
        await colorDictionaryTask;

        // Start the bot
        this.TwitchClient.Connect();

        // Start timers after bot has connected
        this.blueGuyModule.StartTimer();
    }

    public void Dispose()
    {
        this.spotifyModule.Dispose();
        this.soundAlertModule.Dispose();
        this.miscCommandsModule.Dispose();
        this.calculatorModule.Dispose();
        this.emoteSoundModule.Dispose();
        this.blueGuyModule.Dispose();
        this.textToSpeechModule.Dispose();

        this.cancellationTokenSource.Dispose();
        this.TwitchAuthenticationManager.Dispose();
        this.ColorDictionary.Dispose();
    }

    private void Client_OnLog(object sender, OnLogArgs e)
    {
        Console.WriteLine($"{e.DateTime}: {e.BotUsername} - {e.Data}");
    }

    private void Client_OnConnected(object sender, OnConnectedArgs e)
    {
        Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        this.TwitchClient.SendMessage(this.TwitchChannelUsername, "Goofbot is activated and at your service MrDestructoid");
    }

    private async void Client_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
    {
        string message = string.Empty;
        string commandName = e.Command.CommandText.ToLowerInvariant().Trim();
        string commandArgs = e.Command.ArgumentsAsString.Trim();

        bool isReversed = false;

        Command command;
        if (this.CommandDictionary.TryGetCommand(commandName, out command))
        {
            message = await command.ExecuteCommandAsync(commandArgs, e, isReversed);
        }
        else if (this.CommandDictionary.TryGetCommand(Program.ReverseString(commandName), out command))
        {
            isReversed = true;

            string commandArgsReversed = Program.ReverseString(commandArgs);

            message = await command.ExecuteCommandAsync(commandArgsReversed, e, isReversed);
            message = Program.ReverseString(message);
        }

        if (!message.Equals(string.Empty))
        {
            this.TwitchClient.SendMessage(this.TwitchChannelUsername, message);
        }
    }

    private void Client_OnIncorrectLogin(object sender, OnIncorrectLoginArgs e)
    {
    }
}
