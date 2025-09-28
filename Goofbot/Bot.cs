namespace Goofbot;

using Goofbot.Structs;
using Goofbot.UtilClasses;
using ImageMagick;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
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

    private readonly CancellationTokenSource cancellationTokenSource;

    private readonly TwitchClient twitchClient = new ();
    private readonly TwitchAPI twitchChannelAPI = new ();
    private readonly TwitchAPI twitchBotAPI = new ();

    private readonly TwitchAuthenticationManager twitchAuthenticationManager;
    private readonly ChannelPointRedemptionEventSub channelPointRedemptionEventSub;

    private readonly List<GoofbotModule> goofbotModules = [];

    private string twitchChannelID;
    private string twitchBotID;

    public Bot(string twitchBotUsername, string twitchChannelUsername, CancellationTokenSource cancellationTokenSource)
    {
        this.TwitchBotUsername = twitchBotUsername;
        this.TwitchChannelUsername = twitchChannelUsername;
        this.cancellationTokenSource = cancellationTokenSource;

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
        this.twitchAuthenticationManager = new (this, this.twitchClient, this.twitchChannelAPI, this.twitchBotAPI, clientID, clientSecret);

        // Subscribe to TwitchClient events
        this.twitchClient.OnLog += this.Client_OnLog;
        this.twitchClient.OnConnected += this.Client_OnConnected;
        this.twitchClient.OnIncorrectLogin += this.Client_OnIncorrectLogin;
        this.twitchClient.OnMessageReceived += this.Client_OnMessageReceived;
        this.twitchClient.OnChatCommandReceived += this.Client_OnChatCommandReceived;

        // Initialize Magick.NET
        MagickNET.Initialize();

        // Subscribe to Twitch EventSub for Channel Point Redemption
        this.channelPointRedemptionEventSub = new (this.twitchChannelAPI);
        this.EventSubWebsocketClient = this.channelPointRedemptionEventSub.EventSubWebsocketClient;

        // Instantiate modules
        foreach (Type t in GetTypesInNamespace(Assembly.GetExecutingAssembly(), "Goofbot.Modules"))
        {
            if (t.IsSubclassOf(typeof(GoofbotModule)))
            {
                this.goofbotModules.Add((GoofbotModule)Activator.CreateInstance(t, this, t.Name));
            }
        }

        this.CommandDictionary.TryAddCommand(new ("shutdown", this.ShutdownCommand, CommandAccessibilityModifier.StreamerOnly));
    }

    public event EventHandler<OnMessageReceivedArgs> MessageReceived;

    public ColorDictionary ColorDictionary { get; private set; }

    public string StuffFolder { get; private set; }

    public EventSubWebsocketClient EventSubWebsocketClient { get; private set; }

    public static async Task InsertOrUpdateTwitchUserAsync(SqliteConnection sqliteConnection, string userID, string userName)
    {
        using var replaceCommand = sqliteConnection.CreateCommand();
        replaceCommand.CommandText = "REPLACE INTO TwitchUsers VALUES (@UserID, @UserName);";
        replaceCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        replaceCommand.Parameters.AddWithValue("@UserName", userName);
        await replaceCommand.ExecuteNonQueryAsync();
    }

    public static string GetLeaderboardString(List<UserNameAndCount> list, string theThingBeingCounted)
    {
        int i = 0;
        var stringBuilder = new StringBuilder();
        foreach (var user in list)
        {
            string s = user.Count > 1 ? "s" : string.Empty;
            stringBuilder = stringBuilder.Append($"{i + 1}. {user.UserName} - {user.Count} {theThingBeingCounted}{s}");
            if (i < list.Count - 1)
            {
                stringBuilder = stringBuilder.Append(" | ");
            }

            i++;
        }

        return stringBuilder.ToString();
    }

    public async Task StartAsync()
    {
        List<Task> tasks = [];
        tasks.Add(this.ColorDictionary.InitializeAsync());
        tasks.Add(this.twitchAuthenticationManager.InitializeAsync());
        tasks.Add(this.InitializeDatabaseAsync());

        foreach (GoofbotModule module in this.goofbotModules)
        {
            tasks.Add(module.InitializeAsync());
        }

        await Task.WhenAll(tasks);

        // Save Twitch user IDs of channel and bot accounts
        Task<string> twitchChannelIDTask = this.GetUserIDAsync(this.TwitchChannelUsername);
        Task<string> twitchBotIDTask = this.GetUserIDAsync(this.TwitchBotUsername);

        // Requires TwitchAPI to be initialized
        this.channelPointRedemptionEventSub.Start();

        // Requires TwitchClient to be initialized
        this.twitchClient.AddChatCommandIdentifier('!');

        this.twitchChannelID = await twitchChannelIDTask;
        this.twitchBotID = await twitchBotIDTask;

        // Start the bot
        this.twitchClient.Connect();
    }

    public async Task<string> GetUserIDAsync(string userName)
    {
        var response = await this.twitchChannelAPI.Helix.Users.GetUsersAsync(logins: [userName.ToLowerInvariant()]);
        var user = response.Users[0];
        return user.Id;
    }

    public async Task TimeoutUserAsync(string userID, int duration, string reason = "")
    {
        var banUserRequest = new BanUserRequest
        {
            UserId = userID,
            Duration = duration,
            Reason = reason,
        };

        if (userID.Equals(this.twitchChannelID) || userID.Equals(this.twitchBotID))
        {
            return;
        }
        else
        {
            await this.twitchBotAPI.Helix.Moderation.BanUserAsync(this.twitchChannelID, this.twitchBotID, banUserRequest);
        }
    }

    public async Task UpdateRedemptionStatusAsync(string rewardID, string redemptionID, CustomRewardRedemptionStatus status)
    {
        var request = new UpdateCustomRewardRedemptionStatusRequest
        {
            Status = status,
        };

        await this.twitchChannelAPI.Helix.ChannelPoints.UpdateRedemptionStatusAsync(this.twitchChannelID, rewardID, [redemptionID], request);
    }

    public async Task CreateCustomRewardAsync(string rewardName)
    {
        var request = new CreateCustomRewardsRequest
        {
            Cost = 1,
            Title = rewardName,
        };

        await this.twitchChannelAPI.Helix.ChannelPoints.CreateCustomRewardsAsync(this.twitchChannelID, request);
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
        foreach (GoofbotModule module in this.goofbotModules)
        {
            module.Dispose();
        }

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

    private static IEnumerable<Type> GetTypesInNamespace(Assembly assembly, string nameSpace)
    {
        return
          assembly.GetTypes()
                  .Where(t => string.Equals(t.Namespace, nameSpace, StringComparison.Ordinal));
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
        foreach (GoofbotModule module in this.goofbotModules)
        {
            module.StartTimers();
        }
    }

    private async void Client_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
    {
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

    private async Task ShutdownCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        this.SendMessage("Shutting down now MrDestructoid", isReversed);
        await this.cancellationTokenSource.CancelAsync();
    }

    private async Task InitializeDatabaseAsync()
    {
        using (var sqliteConnection = this.OpenSqliteConnection())
        using (var command = sqliteConnection.CreateCommand())
        using (await this.SqliteReaderWriterLock.WriteLockAsync())
        {
            try
            {
                command.CommandText = "PRAGMA journal_mode = wal;";
                await command.ExecuteNonQueryAsync();
                command.CommandText = "PRAGMA foreign_keys = ON;";
                await command.ExecuteNonQueryAsync();
                command.CommandText =
                    @"CREATE TABLE IF NOT EXISTS TwitchUsers (
                        UserID INTEGER PRIMARY KEY,
                        UserName TEXT NOT NULL
                );";
                await command.ExecuteNonQueryAsync();
            }
            catch (SqliteException e)
            {
                Console.WriteLine($"SQLITE EXCEPTION: {e}");
            }
        }
    }
}
