namespace Goofbot;

using Goofbot.Modules;
using Goofbot.UtilClasses;
using ImageMagick;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets;

internal class Bot : IDisposable
{
    public const string DatabaseFile = "data.db";

    public readonly string TwitchBotUsername;
    public readonly string TwitchChannelUsername;

    public readonly AsyncReaderWriterLock SqliteReaderWriterLock = new ();
    public readonly CommandDictionary CommandDictionary = new ();

    private readonly string goofbotAppDataFolder = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Goofbot");

    private readonly TwitchClient twitchClient = new ();
    private readonly TwitchAPI twitchAPI = new ();
    private readonly TwitchAuthenticationManager twitchAuthenticationManager;
    private readonly ChannelPointRedemptionEventSub channelPointRedemptionEventSub;

    private readonly SpotifyModule spotifyModule;
    private readonly SoundAlertModule soundAlertModule;
    private readonly MiscCommandsModule miscCommandsModule;
    private readonly CalculatorModule calculatorModule;
    private readonly EmoteSoundModule emoteSoundModule;
    private readonly BlueGuyModule blueGuyModule;
    private readonly TextToSpeechModule textToSpeechModule;
    private readonly CheckInTokenModule checkInTokenModule;

    public Bot(string twitchBotUsername, string twitchChannelUsername)
    {
        this.TwitchBotUsername = twitchBotUsername;
        this.TwitchChannelUsername = twitchChannelUsername;

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
        this.twitchAuthenticationManager = new (this, this.twitchClient, this.twitchAPI, clientID, clientSecret);

        // Subscribe to TwitchClient events
        this.twitchClient.OnLog += this.Client_OnLog;
        this.twitchClient.OnConnected += this.Client_OnConnected;
        this.twitchClient.OnIncorrectLogin += this.Client_OnIncorrectLogin;
        this.twitchClient.OnMessageReceived += this.Client_OnMessageReceived;
        this.twitchClient.OnChatCommandReceived += this.Client_OnChatCommandReceived;

        // Initialize Magick.NET
        MagickNET.Initialize();

        // Subscribe to Twitch EventSub for Channel Point Redemption
        this.channelPointRedemptionEventSub = new (this.twitchAPI);
        this.EventSubWebsocketClient = this.channelPointRedemptionEventSub.EventSubWebsocketClient;

        // Instantiate modules
        this.spotifyModule = new (this, "SpotifyModule");
        this.soundAlertModule = new (this, "SoundAlertModule");
        this.miscCommandsModule = new (this, "MiscCommandsModule");
        this.calculatorModule = new (this, "CalculatorModule");
        this.emoteSoundModule = new (this, "EmoteSoundModule");
        this.blueGuyModule = new (this, "BlueGuyModule");
        this.textToSpeechModule = new (this, "TextToSpeechModule");
        this.checkInTokenModule = new (this, "CheckInTokenModule");
    }

    public event EventHandler<OnMessageReceivedArgs> MessageReceived;

    public ColorDictionary ColorDictionary { get; private set; }

    public string StuffFolder { get; private set; }

    public EventSubWebsocketClient EventSubWebsocketClient { get; private set; }

    public async Task StartAsync()
    {
        List<Task> tasks = [];
        tasks.Add(this.ColorDictionary.InitializeAsync());
        tasks.Add(this.twitchAuthenticationManager.InitializeAsync());
        tasks.Add(this.spotifyModule.InitializeAsync());
        tasks.Add(this.InitializeDatabaseAsync());
        tasks.Add(this.checkInTokenModule.InitializeAsync());

        await Task.WhenAll(tasks);

        // Requires TwitchAPI to be initialized
        this.channelPointRedemptionEventSub.Start();

        // Requires TwitchClient to be initialized
        this.twitchClient.AddChatCommandIdentifier('!');

        // Start the bot
        this.twitchClient.Connect();
    }

    public void SendMessage(string message, bool reverseMessage)
    {
        if (reverseMessage)
        {
            message = Program.ReverseString(message);
        }

        this.twitchClient.SendMessage(this.TwitchChannelUsername, message);
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
        this.checkInTokenModule.Dispose();

        this.twitchAuthenticationManager.Dispose();
        this.ColorDictionary.Dispose();
    }

    public SqliteConnection OpenSqliteConnection()
    {
        // Create connection to Sqlite database
        SqliteConnectionStringBuilder connectionStringBuilder = [];
        connectionStringBuilder.DataSource = Path.Join(this.StuffFolder, DatabaseFile);
        var sqliteConnection = new SqliteConnection(connectionStringBuilder.ConnectionString);
        sqliteConnection.Open();
        return sqliteConnection;
    }

    public async Task InsertOrUpdateTwitchUserAsync(string userID, string userName)
    {
        using var sqliteConnection = this.OpenSqliteConnection();
        using var replaceCommand = new SqliteCommand(null, sqliteConnection);
        replaceCommand.CommandText = "REPLACE INTO TwitchUsers VALUES (@UserID, @UserName);";
        replaceCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        replaceCommand.Parameters.AddWithValue("@UserName", userName);
        using (await this.SqliteReaderWriterLock.WriteLockAsync())
        {
            await replaceCommand.ExecuteNonQueryAsync();
        }
    }

    private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
    {
        this.MessageReceived?.Invoke(this, e);
    }

    private void Client_OnLog(object sender, OnLogArgs e)
    {
        Console.WriteLine($"{e.DateTime}: {e.BotUsername} - {e.Data}");
    }

    private void Client_OnConnected(object sender, OnConnectedArgs e)
    {
        Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        this.SendMessage("Goofbot is activated and at your service MrDestructoid", false);

        // Start timers after bot has connected
        this.blueGuyModule.StartTimer();
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
            await command.ExecuteCommandAsync(commandArgs, isReversed, e);
        }
        else if (this.CommandDictionary.TryGetCommand(Program.ReverseString(commandName), out command))
        {
            isReversed = true;
            string commandArgsReversed = Program.ReverseString(commandArgs);
            await command.ExecuteCommandAsync(commandArgsReversed, isReversed, e);
        }
    }

    private void Client_OnIncorrectLogin(object sender, OnIncorrectLoginArgs e)
    {
    }

    private async Task InitializeDatabaseAsync()
    {
        using var sqliteConnection = this.OpenSqliteConnection();
        using var command = new SqliteCommand(null, sqliteConnection);
        command.CommandText =
            @"PRAGMA journal_mode = wal;
            PRAGMA journal_mode = wal;
            CREATE TABLE IF NOT EXISTS TwitchUsers (
                UserID INTEGER PRIMARY KEY,
                UserName TEXT NOT NULL
            );";
        using (await this.SqliteReaderWriterLock.WriteLockAsync())
        {
            await command.ExecuteNonQueryAsync();
        }
    }
}
