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

        this.CreateUsersTable();
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        string reward = e.Notification.Payload.Event.Reward.Title.ToLowerInvariant();
        string userID = e.Notification.Payload.Event.UserId;
        string userName = e.Notification.Payload.Event.UserName;

        if (reward.Equals("howdy pardner"))
        {
            this.InsertOrUpdateUser(userID, userName);
        }
    }

    private void CreateUsersTable()
    {
        using var createTable = new SqliteCommand();
        createTable.CommandText =
                @"CREATE TABLE IF NOT EXISTS 
                Users (UserID INTEGER PRIMARY KEY, 
                UserName TEXT NOT NULL)
        ";
        createTable.Connection = this.bot.SqliteConnection;
        createTable.ExecuteReader();
    }

    private void InsertOrUpdateUser(string userID, string userName)
    {
        string insertCommandText = "INSERT INTO Users VALUES (@UserID, @UserName);";
        var insertCommand = new SqliteCommand(insertCommandText, this.bot.SqliteConnection);
        insertCommand.Parameters.AddWithValue("@UserID", int.Parse(userID));
        insertCommand.Parameters.AddWithValue("@UserName", userName);

        try
        {
            insertCommand.ExecuteReader();
            Console.WriteLine("lol1");
        }
        catch
        {
            string updateCommandText =
            @"UPDATE Users
                SET UserName = @UserName
                WHERE UserID = @UserID
        ";
            var updateCommand = new SqliteCommand(updateCommandText, this.bot.SqliteConnection);
            updateCommand.Parameters.AddWithValue("@UserID", int.Parse(userID));
            updateCommand.Parameters.AddWithValue("@UserName", userName);

            updateCommand.ExecuteReader();

            Console.WriteLine("lol2");
        }
    }
}
