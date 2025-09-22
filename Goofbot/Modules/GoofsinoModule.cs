namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Documents;
using TwitchLib.Api.Helix.Models.Charity.GetCharityCampaign;
using TwitchLib.Client.Events;

internal class GoofsinoModule : GoofbotModule
{
    private const string TheHouseID = "-1";
    private const long RouletteMinimumBet = 1;

    private static readonly Bet RouletteColumn1 = new (2, 2, "column 1");
    private static readonly Bet RouletteColumn2 = new (3, 2, "column 2");
    private static readonly Bet RouletteColumn3 = new (4, 2, "column 3");
    private static readonly Bet RouletteDozen1 = new (5, 2, "first dozen");
    private static readonly Bet RouletteDozen2 = new (6, 2, "second dozen");
    private static readonly Bet RouletteDozen3 = new (7, 2, "third dozen");
    private static readonly Bet RouletteHigh = new (8, 1, "high");
    private static readonly Bet RouletteLow = new (9, 1, "low");
    private static readonly Bet RouletteEven = new (10, 1, "even");
    private static readonly Bet RouletteOdd = new (11, 1, "odd");
    private static readonly Bet RouletteRed = new (12, 1, "red");
    private static readonly Bet RouletteBlack = new (13, 1, "black");
    private static readonly Bet RouletteTopLine = new (14, 6, "top line");

    private readonly RouletteTable rouletteTable = new ();

    public GoofsinoModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.bot.CommandDictionary.TryAddCommand(new Command("red", this.RedCommand, unlisted: true, timeoutSeconds: 0));
        this.bot.CommandDictionary.TryAddCommand(new Command("black", this.BlackCommand, unlisted: true, timeoutSeconds: 0));

        this.bot.CommandDictionary.TryAddCommand(new Command("balance", this.BalanceCommand));

        this.bot.CommandDictionary.TryAddCommand(new Command("spin", this.SpinCommand, CommandAccessibilityModifier.StreamerOnly));

    }

    public async Task InitializeAsync()
    {
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        {
            await CreateTablesAsync(sqliteConnection);
        }
    }

    private async Task BalanceCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.Username;

        await this.SetupUserAsync(userID, userName);

        long balance;
        long totalBets;
        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        {
            balance = await GetBalanceAsync(sqliteConnection, userID);
            totalBets = await GetTotalBetsAsync(sqliteConnection, userID);
        }

        if (totalBets > 0)
        {
            this.bot.SendMessage($"@{userName} {balance} gamba points - {totalBets} total bets = {balance - totalBets} points available", isReversed);
        }
        else
        {
            this.bot.SendMessage($"@{userName} {balance} gamba points", isReversed);
        }
    }

    private async Task RedCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, RouletteRed);
    }

    private async Task BlackCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, RouletteBlack);
    }

    private async Task BetCommandHelperAsync(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs, Bet bet)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.Username;

        await this.SetupUserAsync(userID, userName);

        if (long.TryParse(commandArgs, out long amount) && amount >= RouletteMinimumBet)
        {
            long existingBets = 0;
            using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
            using (var sqliteConnection = this.bot.OpenSqliteConnection())
            {
                existingBets = await GetBetAmountAsync(sqliteConnection, userID, bet);
                await TryPlaceBetAsync(sqliteConnection, userID, bet, amount);
            }

            if (existingBets > 0)
            {
                this.bot.SendMessage($"{userName} bet {amount} more on {bet.BetName} for a total of {amount + existingBets}", isReversed);
            }
            else
            {
                this.bot.SendMessage($"{userName} bet {amount} on {bet.BetName}", isReversed);
            }
        }
        else
        {
            this.bot.SendMessage($"Minimum bet: {RouletteMinimumBet}", isReversed);
        }
    }

    private async Task SpinCommand(string commandArgs = "", bool isReversed = false, OnChatCommandReceivedArgs eventArgs = null)
    {
        this.rouletteTable.Spin();
        var color = this.rouletteTable.Color;

        this.bot.SendMessage($"The wheel landed on {this.rouletteTable.LastSpinResult} ({Enum.GetName(color)})", isReversed);

        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        {
            if (color == RouletteTable.RouletteColor.Red)
            {
                await this.ResolveAllBetsByTypeAsync(sqliteConnection, RouletteRed, true);
                await this.ResolveAllBetsByTypeAsync(sqliteConnection, RouletteBlack, false);
            }
            else if (color == RouletteTable.RouletteColor.Black)
            {
                await this.ResolveAllBetsByTypeAsync(sqliteConnection, RouletteRed, false);
                await this.ResolveAllBetsByTypeAsync(sqliteConnection, RouletteBlack, true);
            }
            else
            {
                await this.ResolveAllBetsByTypeAsync(sqliteConnection, RouletteRed, false);
                await this.ResolveAllBetsByTypeAsync(sqliteConnection, RouletteBlack, false);
            }
        }
    }

    private static async Task<bool> CreateTablesAsync(SqliteConnection sqliteConnection)
    {
        using var transaction = sqliteConnection.BeginTransaction();
        try
        {
            using var sqliteCommand = sqliteConnection.CreateCommand();
            sqliteCommand.CommandText =
                @"CREATE TABLE IF NOT EXISTS Bets (
                    UserID INTEGER NOT NULL,
                    BetTypeID INTEGER NOT NULL,
                    Amount INTEGER NOT NULL,
                    PRIMARY KEY (UserID, BetTypeID),
                    FOREIGN KEY(UserID) REFERENCES TwitchUsers(UserID)
                ) WITHOUT ROWID;";
            await sqliteCommand.ExecuteNonQueryAsync();

            sqliteCommand.CommandText = "CREATE INDEX IF NOT EXISTS BetsIDsIdx ON Bets (BetTypeID);";
            await sqliteCommand.ExecuteNonQueryAsync();

            sqliteCommand.CommandText =
                @"CREATE TABLE IF NOT EXISTS GambaPoints (
                    UserID INTEGER PRIMARY KEY,
                    Balance INTEGER NOT NULL,
                    LastUpdateTimestamp REAL NOT NULL,
                    FOREIGN KEY(UserID) REFERENCES TwitchUsers(UserID)
                );";
            await sqliteCommand.ExecuteNonQueryAsync();

            sqliteCommand.CommandText = "CREATE INDEX IF NOT EXISTS GambaPointsBalanceIdx ON GambaPoints (Balance DESC, LastUpdateTimestamp ASC);";
            await sqliteCommand.ExecuteNonQueryAsync();

            sqliteCommand.CommandText =
                @"CREATE TABLE IF NOT EXISTS Bankruptcies (
                    UserID INTEGER PRIMARY KEY,
                    Count INTEGER NOT NULL,
                    LastUpdateTimestamp REAL NOT NULL,
                    FOREIGN KEY(UserID) REFERENCES TwitchUsers(UserID)
                );";
            await sqliteCommand.ExecuteNonQueryAsync();

            sqliteCommand.CommandText = "CREATE INDEX IF NOT EXISTS BankruptcyCountsIdx ON Bankruptcies (Count DESC, LastUpdateTimestamp ASC);";
            await sqliteCommand.ExecuteNonQueryAsync();

            sqliteCommand.CommandText = "INSERT INTO TwitchUsers VALUES (-1, 'The House') ON CONFLICT DO NOTHING;";
            await sqliteCommand.ExecuteNonQueryAsync();

            sqliteCommand.CommandText = "INSERT INTO GambaPoints VALUES (-1, 0, unixepoch('now','subsec')) ON CONFLICT DO NOTHING;";
            await sqliteCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch (SqliteException)
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    private static async Task AddUserToGambaPointsTableAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "INSERT INTO GambaPoints VALUES (@UserID, 1000, unixepoch('now','subsec')) ON CONFLICT DO NOTHING;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ResetUserGambaPointsBalanceAndIncrementBankruptciesAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var transaction = sqliteConnection.BeginTransaction();
        try
        {
            using var replaceCommand = sqliteConnection.CreateCommand();
            replaceCommand.CommandText = "REPLACE INTO GambaPoints VALUES (@UserID, 1000, unixepoch('now','subsec'));";
            replaceCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

            using var updateCommand = sqliteConnection.CreateCommand();
            updateCommand.CommandText =
                @"INSERT INTO Bankruptcies VALUES (@UserID, 1, unixepoch('now','subsec'))
                            ON CONFLICT(UserID) DO UPDATE SET Count = Count + 1;";
            updateCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

            await replaceCommand.ExecuteNonQueryAsync();
            await updateCommand.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (SqliteException)
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    private static async Task<bool> ResolveBetAsync(SqliteConnection sqliteConnection, string userID, Bet bet, bool success)
    {
        using var transaction = sqliteConnection.BeginTransaction();
        try
        {
            long amount = await GetBetAmountAsync(sqliteConnection, userID, bet);
            await DeleteBetFromTableAsync(sqliteConnection, userID, bet);


            if (success)
            {
                amount *= bet.PayoutRatio;
            }
            else
            {
                amount *= -1;
            }

            await AddBalanceAsync(sqliteConnection, userID, amount);

            amount *= -1;
            await AddBalanceAsync(sqliteConnection, TheHouseID, amount);

            await transaction.CommitAsync();
            return true;
        }
        catch (SqliteException)
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    private static async Task<SqliteDataReader> GetAllBetsByTypeAsync(SqliteConnection sqliteConnection, Bet bet)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT Bets.UserID, TwitchUsers.UserName, Bets.Amount FROM Bets INNER JOIN TwitchUsers ON TwitchUsers.UserID = Bets.UserID WHERE BetTypeID = @BetTypeID;";
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);

        return await sqliteCommand.ExecuteReaderAsync();
    }

    private static async Task DeleteAllBetsByType(SqliteConnection sqliteConnection, Bet bet)
    {
        using var deleteCommand = sqliteConnection.CreateCommand();
        deleteCommand.CommandText = "DELETE FROM Bets WHERE BetTypeID = @BetTypeID;";
        deleteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);
        await deleteCommand.ExecuteNonQueryAsync();
    }

    private async Task<bool> ResolveAllBetsByTypeAsync(SqliteConnection sqliteConnection, Bet bet, bool success)
    {
        using var transaction = sqliteConnection.BeginTransaction();
        try
        {
            List<string> messages = [];
            var reader = await GetAllBetsByTypeAsync(sqliteConnection, bet);

            while (await reader.ReadAsync())
            {
                string userID = reader.GetInt64(0).ToString();
                string userName = reader.GetString(1);
                long amount = reader.GetInt64(2);
                string verb;

                if (success)
                {
                    verb = "won";
                    amount *= bet.PayoutRatio;
                }
                else
                {
                    verb = "lost";
                    amount *= -1;
                }

                messages.Add($"{userName} {verb} {Math.Abs(amount)} gamba points for betting on {bet.BetName}");

                await AddBalanceAsync(sqliteConnection, userID, amount);

                amount *= -1;
                await AddBalanceAsync(sqliteConnection, TheHouseID, amount);
            }

            await DeleteAllBetsByType(sqliteConnection, bet);

            await transaction.CommitAsync();
            return true;
        }
        catch (SqliteException)
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    private static async Task<long> GetBalanceAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT Balance FROM GambaPoints WHERE UserID = @UserID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    private static async Task<long> GetTotalBetsAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT SUM(Amount) FROM Bets WHERE UserID = @UserID GROUP BY UserID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    private static async Task<long> GetBetAmountAsync(SqliteConnection sqliteConnection, string userID, Bet bet)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT Amount FROM Bets WHERE UserID = @UserID AND BetTypeID = @BetTypeID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    private static async Task AddBetToTableAsync(SqliteConnection sqliteConnection, string userID, Bet bet, long amount)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "INSERT INTO Bets VALUES (@UserID, @BetTypeID, @Amount) ON CONFLICT DO UPDATE SET Amount = Amount + @Amount;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);
        sqliteCommand.Parameters.AddWithValue("@Amount", amount);

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    private static async Task AddBalanceAsync(SqliteConnection sqliteConnection, string userID, long amount)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "UPDATE GambaPoints SET Balance = Balance + @Amount, LastUpdateTimestamp = unixepoch('now','subsec') WHERE UserID = @UserID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@Amount", amount);

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    private static async Task DeleteBetFromTableAsync(SqliteConnection sqliteConnection, string userID, Bet bet)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "DELTE FROM Bets WHERE UserID = @UserID AND BetTypeID = @BetTypeID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TryPlaceBetAsync(SqliteConnection sqliteConnection, string userID, Bet bet, long amount)
    {
        if (amount < 0)
        {
            return false;
        }

        /*using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        {*/
        var balanceTask = GetBalanceAsync(sqliteConnection, userID);
        var totalBetsTask = GetTotalBetsAsync(sqliteConnection, userID);

        long balance = await balanceTask;
        long totalBets = await totalBetsTask;

        if (amount + totalBets <= balance)
        {
            await AddBetToTableAsync(sqliteConnection, userID, bet, amount);
            return true;
        }
        else
        {
            return false;
        }
        //}
    }

    private async Task SetupUserAsync(string userID, string userName)
    {
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        {
            await Bot.InsertOrUpdateTwitchUserAsync(sqliteConnection, userID, userName);
            await AddUserToGambaPointsTableAsync(sqliteConnection, userID);
        }
    }

    private async Task<bool> TryDeclareBankruptcy(string userID)
    {
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        {
            var balanceTask = GetBalanceAsync(sqliteConnection, userID);
            var totalBetsTask = GetTotalBetsAsync(sqliteConnection, userID);

            long balance = await balanceTask;
            long totalBets = await totalBetsTask;

            if (totalBets + balance <= 0)
            {
                await ResetUserGambaPointsBalanceAndIncrementBankruptciesAsync(sqliteConnection, userID);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    private readonly struct Bet(long typeID, long payoutRatio, string betName)
    {
        public readonly long TypeID = typeID;
        public readonly long PayoutRatio = payoutRatio;
        public readonly string BetName = betName;
    }
}
