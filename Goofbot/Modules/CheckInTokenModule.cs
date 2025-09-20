namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using Microsoft.Data.Sqlite;
using System;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

internal class CheckInTokenModule : GoofbotModule
{
    public CheckInTokenModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.bot.EventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += this.OnChannelPointsCustomRewardRedemptionAdd;
    }

    public async Task InitializeAsync()
    {
        await this.CreateTokenCountsTableAsync();
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        string reward = e.Notification.Payload.Event.Reward.Title;
        string userID = e.Notification.Payload.Event.UserId;
        string userName = e.Notification.Payload.Event.UserName;

        if (reward.Equals("Daily GoofCoin", StringComparison.OrdinalIgnoreCase))
        {
            await this.bot.InsertOrUpdateTwitchUserAsync(userID, userName);
            long tokens = await this.IncrementTokensAsync(userID);
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

    private async Task<long> IncrementTokensAsync(string userID)
    {
        using var sqliteConnection = this.bot.OpenSqliteConnection();

        using var updateCommand = new SqliteCommand(null, sqliteConnection);
        updateCommand.CommandText =
            @"INSERT INTO TokenCounts VALUES (@UserID, 1, unixepoch('now','subsec'))
                ON CONFLICT(UserID) DO UPDATE SET TokenCount = TokenCount + 1;";
        updateCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        using var selectCommand = new SqliteCommand(null, sqliteConnection);
        selectCommand.CommandText = "Select TokenCount FROM TokenCounts WHERE UserID = @UserID;";
        selectCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        long result;
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            await updateCommand.ExecuteNonQueryAsync();
            result = Convert.ToInt64(await selectCommand.ExecuteScalarAsync());
        }

        return result;
    }
}
