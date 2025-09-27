namespace Goofbot.Modules;

using Goofbot.Structs;
using Goofbot.UtilClasses;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

internal class CheckInTokenModule : GoofbotModule
{
    public CheckInTokenModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.bot.EventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += this.OnChannelPointsCustomRewardRedemptionAdd;

        this.bot.CommandDictionary.TryAddCommand(new Command("coinboard", this.GoofCoinLeaderboardCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("coins", this.CoinsCommand));
    }

    public async Task InitializeAsync()
    {
        await this.CreateTokenCountsTableAsync();
    }

    private static async Task<long> IncrementTokensAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var updateCommand = sqliteConnection.CreateCommand();
        updateCommand.CommandText =
            @"INSERT INTO TokenCounts VALUES (@UserID, 1, unixepoch('now','subsec'))
                ON CONFLICT(UserID) DO UPDATE SET TokenCount = TokenCount + 1;";
        updateCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        using var selectCommand = sqliteConnection.CreateCommand();
        selectCommand.CommandText = "Select TokenCount FROM TokenCounts WHERE UserID = @UserID;";
        selectCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        await updateCommand.ExecuteNonQueryAsync();
        return Convert.ToInt64(await selectCommand.ExecuteScalarAsync());
    }

    private static async Task<long> GetTokenCount(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT TokenCount FROM TokenCounts WHERE UserID = @UserID";
        sqliteCommand.Parameters.AddWithValue("@UserID", userID);

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    private async Task CoinsCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.DisplayName;

        long coins;
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            await Bot.InsertOrUpdateTwitchUserAsync(sqliteConnection, userID, userName);
            coins = await GetTokenCount(sqliteConnection, userID);
        }

        string s = coins == 1 ? string.Empty : "s";
        this.bot.SendMessage($"{userName} has {coins} GoofCoin{s}", isReversed);
    }

    private async Task GoofCoinLeaderboardCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        List<UserNameAndCount> leaderboardEntries = [];

        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        using (var sqliteCommand = sqliteConnection.CreateCommand())
        {
            sqliteCommand.CommandText =
                @$"SELECT TwitchUsers.UserName, TokenCounts.TokenCount
                    FROM TwitchUsers
                    INNER JOIN TokenCounts ON TwitchUsers.UserID = TokenCounts.UserID
                    ORDER BY TokenCounts.TokenCount DESC, TokenCounts.LastUpdateTimestamp ASC LIMIT 5;
            ";

            using var reader = await sqliteCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                leaderboardEntries.Add(new UserNameAndCount(reader.GetString(0), reader.GetInt64(1)));
            }
        }

        int i = 0;
        var stringBuilder = new StringBuilder();
        foreach (var user in leaderboardEntries)
        {
            string s = user.Count > 1 ? "s" : string.Empty;
            stringBuilder = stringBuilder.Append($"{i + 1}. {user.UserName} - {user.Count} GoofCoin{s}");
            if (i < leaderboardEntries.Count - 1)
            {
                stringBuilder = stringBuilder.Append(" | ");
            }

            i++;
        }

        this.bot.SendMessage(stringBuilder.ToString(), isReversed);
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        string reward = e.Notification.Payload.Event.Reward.Title;
        string userID = e.Notification.Payload.Event.UserId;
        string userName = e.Notification.Payload.Event.UserName;

        if (reward.Equals("Daily GoofCoin", StringComparison.OrdinalIgnoreCase))
        {
            long tokens;
            using (var sqliteConnection = this.bot.OpenSqliteConnection())
            using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
            {
                await Bot.InsertOrUpdateTwitchUserAsync(sqliteConnection, userID, userName);
                tokens = await IncrementTokensAsync(sqliteConnection, userID);
            }

            if (tokens == 1)
            {
                this.bot.SendMessage($"Congrats on your very first GoofCoin, {userName}!", false);
            }
            else
            {
                this.bot.SendMessage($"Congrats, {userName}, you now have {tokens} GoofCoins!", false);
            }
        }
    }

    private async Task CreateTokenCountsTableAsync()
    {
        using var sqliteConnection = this.bot.OpenSqliteConnection();
        using var createTableCommand = new SqliteCommand(null, sqliteConnection);
        createTableCommand.CommandText =
            @"CREATE TABLE IF NOT EXISTS TokenCounts (
                UserID INTEGER PRIMARY KEY,
                TokenCount INTEGER NOT NULL,
                LastUpdateTimestamp REAL NOT NULL,
                FOREIGN KEY(UserID) REFERENCES TwitchUsers(UserID)
            );

            CREATE INDEX IF NOT EXISTS TokenCountsIdx ON TokenCounts (TokenCount DESC, LastUpdateTimestamp ASC);";
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            await createTableCommand.ExecuteNonQueryAsync();
        }
    }


}
