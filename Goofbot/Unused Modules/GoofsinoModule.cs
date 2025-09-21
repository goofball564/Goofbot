namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using Microsoft.Data.Sqlite;
using System;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.Charity.GetCharityCampaign;
using TwitchLib.Client.Events;

internal class GoofsinoModule : GoofbotModule
{
    private const string TheHouseID = "-1";
    private const long RouletteMinimumBet = 1;

    private static readonly Bet RouletteColumn1 = new (2, 2);
    private static readonly Bet RouletteColumn2 = new (3, 2);
    private static readonly Bet RouletteColumn3 = new (4, 2);
    private static readonly Bet RouletteDozen1 = new (5, 2);
    private static readonly Bet RouletteDozen2 = new (6, 2);
    private static readonly Bet RouletteDozen3 = new (7, 2);
    private static readonly Bet RouletteHigh = new (8, 1);
    private static readonly Bet RouletteLow = new (9, 1);
    private static readonly Bet RouletteEven = new (10, 1);
    private static readonly Bet RouletteOdd = new (11, 1);
    private static readonly Bet RouletteRed = new (12, 1);
    private static readonly Bet RouletteBlack = new (13, 1);
    private static readonly Bet RouletteTopLine = new (14, 6);

    private readonly RouletteTable rouletteTable = new ();

    public GoofsinoModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.bot.CommandDictionary.TryAddCommand(new Command("red", this.RedCommand, unlisted: true, timeoutSeconds: 0));
        this.bot.CommandDictionary.TryAddCommand(new Command("black", this.BlackCommand, unlisted: true, timeoutSeconds: 0));

        this.bot.CommandDictionary.TryAddCommand(new Command("spin", this.SpinCommand, CommandAccessibilityModifier.StreamerOnly));
    }

    private async Task RedCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.Username;

        await this.SetupUserAsync(userID, userName);

        if (long.TryParse(commandArgs, out long amount) && amount >= RouletteMinimumBet)
        {
            await this.TryPlaceBetAsync(userID, RouletteRed, amount);
            this.bot.SendMessage($"{userName} has bet {amount} on red.", isReversed);
        }
        else
        {
            this.bot.SendMessage($"Minimum bet: {RouletteMinimumBet}", isReversed);
        }
    }

    private async Task BlackCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.Username;

        await this.SetupUserAsync(userID, userName);

        if (long.TryParse(commandArgs, out long amount) && amount >= RouletteMinimumBet)
        {
            await this.TryPlaceBetAsync(userID, RouletteBlack, amount);
            this.bot.SendMessage($"{userName} has bet {amount} on black.", isReversed);
        }
        else
        {
            this.bot.SendMessage($"Minimum bet: {RouletteMinimumBet}", isReversed);
        }
    }

    private async Task SpinCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        this.rouletteTable.Spin();

        var color = this.rouletteTable.Color;
        if (color == RouletteTable.RouletteColor.Red)
        {
            // red bets succeed, black bets fail
        }
        else if (color == RouletteTable.RouletteColor.Black)
        {
            // black bets succeed, red bets fail
        }
    }

    public async Task InitializeAsync()
    {
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        {
            await CreateTablesAsync(sqliteConnection);
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
                ) WITHOUT ROWID;

                CREATE INDEX IF NOT EXISTS BetsIDsIdx ON Bets (BetTypeID);

                CREATE TABLE IF NOT EXISTS GambaPoints (
                    UserID INTEGER PRIMARY KEY,
                    Balance INTEGER NOT NULL,
                    LastUpdateTimestamp REAL NOT NULL,
                    FOREIGN KEY(UserID) REFERENCES TwitchUsers(UserID)
                );

                CREATE INDEX IF NOT EXISTS GambaPointsBalanceIdx ON GambaPoints (Balance DESC, LastUpdateTimestamp ASC);

                CREATE TABLE IF NOT EXISTS Bankruptcies (
                    UserID INTEGER PRIMARY KEY,
                    Count INTEGER NOT NULL,
                    LastUpdateTimestamp REAL NOT NULL,
                    FOREIGN KEY(UserID) REFERENCES TwitchUsers(UserID)
                );

                CREATE INDEX IF NOT EXISTS BankruptcyCountsIdx ON Bankruptcies (Count DESC, LastUpdateTimestamp ASC);

                INSERT INTO TwitchUsers VALUES (-1, 'The House') ON CONFLICT DO NOTHING;
                INSERT INTO GambaPoints VALUES (-1, 0, unixepoch('now','subsec') ON CONFLICT DO NOTHING;
            ";

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
        sqliteCommand.CommandText = "UPDATE GambaPoints SET Balance = Balance + @Amount WHERE UserID = @UserID;";
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

    private async Task ResolveAllBetsByType(Bet bet, bool success)
    {
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        {

        }
    }

    private async Task<bool> TryPlaceBetAsync(string userID, Bet bet, long amount)
    {
        if (amount < 0)
        {
            return false;
        }

        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        {
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
        }
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

    private readonly struct Bet(long typeID, long payoutRatio)
    {
        public readonly long TypeID = typeID;
        public readonly long PayoutRatio = payoutRatio;
    }
}
