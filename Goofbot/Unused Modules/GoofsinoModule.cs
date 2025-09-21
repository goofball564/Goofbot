namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using Microsoft.Data.Sqlite;
using System;
using System.Threading.Tasks;

internal class GoofsinoModule : GoofbotModule
{
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

    public GoofsinoModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
    }

    public async Task InitializeAsync()
    {
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            await CreateTablesAsync(sqliteConnection);
        }
    }

    private static async Task<bool> CreateTablesAsync(SqliteConnection sqliteConnection)
    {
        using var transaction = sqliteConnection.BeginTransaction();
        try
        {
            using var sqliteCommand = new SqliteCommand(null, sqliteConnection);
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
        using var sqliteCommand = new SqliteCommand(null, sqliteConnection);
        sqliteCommand.CommandText = "INSERT INTO GambaPoints VALUES (@UserID, 1000, unixepoch('now','subsec')) ON CONFLICT DO NOTHING;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ResetUserGambaPointsBalanceAndIncrementBankruptciesAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var transaction = sqliteConnection.BeginTransaction();
        try
        {
            using var replaceCommand = new SqliteCommand(null, sqliteConnection);
            replaceCommand.CommandText = "REPLACE INTO GambaPoints VALUES (@UserID, 1000, unixepoch('now','subsec'));";
            replaceCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

            using var updateCommand = new SqliteCommand(null, sqliteConnection);
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
        using var sqliteCommand = new SqliteCommand(null, sqliteConnection);
        sqliteCommand.CommandText = "SELECT Balance FROM GambaPoints WHERE UserID = @UserID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    private static async Task<long> GetTotalBetsAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = new SqliteCommand(null, sqliteConnection);
        sqliteCommand.CommandText = "SELECT SUM(Amount) FROM Bets WHERE UserID = @UserID GROUP BY UserID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    private static async Task<long> GetBetAmountAsync(SqliteConnection sqliteConnection, string userID, Bet bet)
    {
        using var sqliteCommand = new SqliteCommand(null, sqliteConnection);
        sqliteCommand.CommandText = "SELECT Amount FROM Bets WHERE UserID = @UserID AND BetTypeID = @BetTypeID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    private static async Task AddBetToBetsTableAsync(SqliteConnection sqliteConnection, string userID, Bet bet, long amount)
    {
        using var sqliteCommand = new SqliteCommand(null, sqliteConnection);
        sqliteCommand.CommandText = "INSERT INTO Bets VALUES (@UserID, @BetTypeID, @Amount) ON CONFLICT DO UPDATE SET Amount = Amount + @Amount;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);
        sqliteCommand.Parameters.AddWithValue("@Amount", amount);

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    private static async Task AddBalanceAsync(SqliteConnection sqliteConnection, string userID, long amount)
    {
        using var sqliteCommand = new SqliteCommand(null, sqliteConnection);
        sqliteCommand.CommandText = "UPDATE GambaPoints SET Balance = Balance + @Amount WHERE UserID = @UserID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@Amount", amount);

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    private async Task<bool> TryPlaceBetAsync(string userID, Bet bet, long amount)
    {
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = new SqliteConnection())
        {
            long balance = await GetBalanceAsync(sqliteConnection, userID);
            long totalBets = await GetTotalBetsAsync(sqliteConnection, userID);

            if (amount + totalBets <= balance)
            {
                await AddBetToBetsTableAsync(sqliteConnection, userID, bet, amount);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    private async Task<bool> TryDeclareBankruptcy(string userID)
    {
        return true;
    }

    private readonly struct Bet(long typeID, long payoutRatio)
    {
        public readonly long TypeID = typeID;
        public readonly long PayoutRatio = payoutRatio;
    }
}
