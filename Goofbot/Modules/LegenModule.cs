namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class LegenModule : GoofbotModule
{
    public LegenModule(Bot bot, string moduleDataFolder)
        : base (bot, moduleDataFolder)
    {
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("legen", this.LegenCommand));
        this.bot.MessageReceived += this.Bot_MessageReceived;
    }

    private async void Bot_MessageReceived(object sender, OnMessageReceivedArgs e)
    {
        string userID = e.ChatMessage.UserId;
        string userName = e.ChatMessage.DisplayName;

        using var sqliteConnection = this.bot.OpenSqliteConnection();

        double timestamp;
        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        {
            timestamp = await GetUserTimestampAsync(sqliteConnection, userID);
        }

        if (timestamp <= 0)
        {
            timestamp = double.MaxValue;
        }

        var utcNow = DateTimeOffset.UtcNow;
        var utc24HoursAgo = utcNow.AddHours(-24);
        long utc24HoursAgoTimestamp = utc24HoursAgo.ToUnixTimeSeconds();

        if (timestamp < utc24HoursAgoTimestamp)
        {
            using (var sqliteTransaction = sqliteConnection.BeginTransaction())
            using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
            {
                try
                {
                    await RemoveUserAsync(sqliteConnection, userID);
                    await sqliteTransaction.CommitAsync();

                    this.bot.SendMessage($"@{userName} ...DARY", false);
                }
                catch
                {
                    await sqliteTransaction.RollbackAsync();
                }
            }
        }
    }

    public override async Task InitializeAsync()
    {
        await this.CreateTableAsync();
        await base.InitializeAsync();
    }

    private async Task LegenCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.DisplayName;

        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var sqliteTransaction = sqliteConnection.BeginTransaction())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            try
            {
                await Bot.InsertOrUpdateTwitchUserAsync(sqliteConnection, userID, userName);

                double timestamp = await GetUserTimestampAsync(sqliteConnection, userID);

                if (timestamp <= 0)
                {
                    await SetUserTimestampAsync(sqliteConnection, userID);
                    this.bot.SendMessage($"@{userName} this is gonna be legen... wait for it...", isReversed);
                }
                else
                {
                    this.bot.SendMessage($"@{userName} wait for it...", isReversed);
                }

                await sqliteTransaction.CommitAsync();
            }
            catch (Exception e)
            {
                await sqliteTransaction.RollbackAsync();
                this.bot.SendMessage("GOOF BOT BROKE (Legen Command)", false);
                Console.WriteLine($"LEGEN COMMAND EXCEPTION\n{e}");
            }
        }
    }

    private async Task CreateTableAsync()
    {
        using var sqliteConnection = this.bot.OpenSqliteConnection();
        using var createTableCommand = sqliteConnection.CreateCommand();
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            createTableCommand.CommandText =
            @"CREATE TABLE IF NOT EXISTS LegenUsers (
                UserID INTEGER PRIMARY KEY,
                Timestamp REAL NOT NULL,
                FOREIGN KEY(UserID) REFERENCES TwitchUsers(UserID)
            );";

            await createTableCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task SetUserTimestampAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var updateCommand = sqliteConnection.CreateCommand();
        updateCommand.CommandText =
            @"REPLACE INTO LegenUsers VALUES (@UserID, unixepoch('now','subsec'));";
        updateCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        await updateCommand.ExecuteNonQueryAsync();
    }

    private static async Task<double> GetUserTimestampAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT Timestamp FROM LegenUsers WHERE UserID = @UserID";
        sqliteCommand.Parameters.AddWithValue("@UserID", userID);

        return Convert.ToDouble(await sqliteCommand.ExecuteScalarAsync());
    }

    private static async Task RemoveUserAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var updateCommand = sqliteConnection.CreateCommand();
        updateCommand.CommandText =
            "DELETE FROM LegenUsers WHERE UserID = @UserID;";
        updateCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        await updateCommand.ExecuteNonQueryAsync();
    }
}
