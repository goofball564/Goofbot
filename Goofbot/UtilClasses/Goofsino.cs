namespace Goofbot.UtilClasses;

using Goofbot.Structs;
using Goofbot.UtilClasses.Bets;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class Goofsino
{
    public static readonly RouletteBet RouletteColumn1 = new (2, 2, "column 1");
    public static readonly RouletteBet RouletteColumn2 = new (3, 2, "column 2");
    public static readonly RouletteBet RouletteColumn3 = new (4, 2, "column 3");
    public static readonly RouletteBet RouletteDozen1 = new (5, 2, "first dozen");
    public static readonly RouletteBet RouletteDozen2 = new (6, 2, "second dozen");
    public static readonly RouletteBet RouletteDozen3 = new (7, 2, "third dozen");
    public static readonly RouletteBet RouletteHigh = new (8, 1, "high");
    public static readonly RouletteBet RouletteLow = new (9, 1, "low");
    public static readonly RouletteBet RouletteEven = new (10, 1, "even");
    public static readonly RouletteBet RouletteOdd = new (11, 1, "odd");
    public static readonly RouletteBet RouletteRed = new (12, 1, "red");
    public static readonly RouletteBet RouletteBlack = new (13, 1, "black");
    public static readonly RouletteBet RouletteTopLine = new (14, 6, "top line");
    public static readonly RouletteBet RouletteGreen = new (15, 17, "green");

    public static readonly BaccaratBet BaccaratPlayer = new (16, 1, "player");
    public static readonly BaccaratBet BaccaratBanker = new (17, 0.95, "banker");
    public static readonly BaccaratBet BaccaratTie = new (18, 8, "a tie");

    public static readonly BlackjackBet Blackjack = new (19, 1, "blackjack");
    public static readonly BlackjackBet BlackjackSplit = new (20, 1, "their second hand");

    private const string TheHouseID = "-1";

    public static async Task CreateTablesAsync(SqliteConnection sqliteConnection)
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

        sqliteCommand.CommandText = $"INSERT INTO TwitchUsers VALUES ({TheHouseID}, 'The House') ON CONFLICT DO NOTHING;";
        await sqliteCommand.ExecuteNonQueryAsync();

        sqliteCommand.CommandText = $"INSERT INTO GambaPoints VALUES ({TheHouseID}, 0, unixepoch('now','subsec')) ON CONFLICT DO NOTHING;";
        await sqliteCommand.ExecuteNonQueryAsync();
    }

    public static async Task<long> GetBankruptcyCountAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT Count from Bankruptcies WHERE UserID = @UserID";
        sqliteCommand.Parameters.AddWithValue("@UserID", userID);

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    public static async Task AddUserToGambaPointsTableAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "INSERT INTO GambaPoints VALUES (@UserID, 1000, unixepoch('now','subsec')) ON CONFLICT DO NOTHING;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    public static async Task<long> GetHouseBalance(SqliteConnection sqliteConnection)
    {
        return await GetBalanceAsync(sqliteConnection, TheHouseID);
    }

    public static async Task<List<UserNameAndCount>> GetTopGambaPointsUsersAsync(SqliteConnection sqliteConnection)
    {
        List<UserNameAndCount> leaderboardEntries = [];

        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText =
            @$"SELECT TwitchUsers.UserName, GambaPoints.Balance
                    FROM TwitchUsers
                    INNER JOIN GambaPoints ON TwitchUsers.UserID = GambaPoints.UserID
                    ORDER BY GambaPoints.Balance DESC, GambaPoints.LastUpdateTimestamp ASC
                    LIMIT 5;
            ";

        using var reader = await sqliteCommand.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            leaderboardEntries.Add(new UserNameAndCount(reader.GetString(0), reader.GetInt64(1)));
        }

        return leaderboardEntries;
    }

    public static async Task<List<string>> ResolveAllBetsByTypeAsync(SqliteConnection sqliteConnection, Bet bet, bool success)
    {
        List<string> messages = [];

        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT Bets.UserID, TwitchUsers.UserName, Bets.Amount FROM Bets INNER JOIN TwitchUsers ON TwitchUsers.UserID = Bets.UserID WHERE BetTypeID = @BetTypeID;";
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);

        using (var reader = await sqliteCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                string userID = reader.GetInt64(0).ToString();
                string userName = reader.GetString(1);
                long amount = reader.GetInt64(2);

                messages.Add(await ResolveBetHelperAsync(sqliteConnection, userID, userName, amount, bet, success));
            }
        }

        await DeleteAllBetsByTypeAsync(sqliteConnection, bet);

        return messages;
    }

    public static async Task ResetUserGambaPointsBalanceAndIncrementBankruptciesAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var replaceCommand = sqliteConnection.CreateCommand();
        replaceCommand.CommandText = "REPLACE INTO GambaPoints VALUES (@UserID, 1000, unixepoch('now','subsec'));";
        replaceCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        using var updateCommand = sqliteConnection.CreateCommand();
        updateCommand.CommandText =
            @"INSERT INTO Bankruptcies VALUES (@UserID, 1, unixepoch('now','subsec'))
                            ON CONFLICT(UserID) DO UPDATE SET Count = Count + 1, LastUpdateTimestamp = unixepoch('now','subsec');";
        updateCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        await replaceCommand.ExecuteNonQueryAsync();
        await updateCommand.ExecuteNonQueryAsync();
    }

    public static async Task<string> ResolveBetAsync(SqliteConnection sqliteConnection, string userID, Bet bet, bool success)
    {
        long amount = await GetBetAmountAsync(sqliteConnection, userID, bet);
        string userName = await Bot.GetUserNameAsync(sqliteConnection, userID);
        await DeleteBetFromTableAsync(sqliteConnection, userID, bet);

        return await ResolveBetHelperAsync(sqliteConnection, userID, userName, amount, bet, success);
    }

    public static async Task DeleteAllBetsByTypeAsync(SqliteConnection sqliteConnection, Bet bet)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "DELETE FROM Bets WHERE BetTypeID = @BetTypeID;";
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);
        await sqliteCommand.ExecuteNonQueryAsync();
    }

    public static async Task<long> GetBalanceAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT Balance FROM GambaPoints WHERE UserID = @UserID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    public static async Task<long> GetTotalBetsAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT SUM(Amount) FROM Bets WHERE UserID = @UserID GROUP BY UserID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    public static async Task<long> GetBetAmountAsync(SqliteConnection sqliteConnection, string userID, Bet bet)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT Amount FROM Bets WHERE UserID = @UserID AND BetTypeID = @BetTypeID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    public static async Task AddBetToTableAsync(SqliteConnection sqliteConnection, string userID, Bet bet, long amount)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "INSERT INTO Bets VALUES (@UserID, @BetTypeID, @Amount) ON CONFLICT DO UPDATE SET Amount = Amount + @Amount;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);
        sqliteCommand.Parameters.AddWithValue("@Amount", amount);

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    public static async Task AddBalanceAsync(SqliteConnection sqliteConnection, string userID, long amount)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "UPDATE GambaPoints SET Balance = Balance + @Amount, LastUpdateTimestamp = unixepoch('now','subsec') WHERE UserID = @UserID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@Amount", amount);

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    public static async Task DeleteBetFromTableAsync(SqliteConnection sqliteConnection, string userID, Bet bet)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "DELETE FROM Bets WHERE UserID = @UserID AND BetTypeID = @BetTypeID;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    public static async Task<bool> TryPlaceBetAsync(SqliteConnection sqliteConnection, string userID, Bet bet, long amount)
    {
        if (amount < 0)
        {
            return false;
        }

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

    public static async Task SetupUserIfNotSetUpAsync(SqliteConnection sqliteConnection, string userID, string userName)
    {
        await Bot.InsertOrUpdateTwitchUserAsync(sqliteConnection, userID, userName);
        await AddUserToGambaPointsTableAsync(sqliteConnection, userID);
    }

    private static async Task<string> ResolveBetHelperAsync(SqliteConnection sqliteConnection, string userID, string userName, long amount, Bet bet, bool success)
    {
        string verb;
        if (success)
        {
            verb = "won";
            amount = Convert.ToInt64(Math.Floor(amount * bet.PayoutRatio));
        }
        else
        {
            verb = "lost";
            amount *= -1;
        }

        long balance = await GetBalanceAsync(sqliteConnection, userID);

        await AddBalanceAsync(sqliteConnection, userID, amount);

        string message = $"{userName} {verb} {Math.Abs(amount)} gamba points for betting on {bet.BetName}. Balance: {balance + amount} gamba points";

        amount *= -1;
        await AddBalanceAsync(sqliteConnection, TheHouseID, amount);

        return message;
    }
}
