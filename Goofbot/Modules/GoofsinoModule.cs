namespace Goofbot.Modules;

using Goofbot.Structs;
using Goofbot.UtilClasses;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class GoofsinoModule : GoofbotModule
{
    private const long RouletteMinimumBet = 1;
    private const string TheHouseID = "-1";

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
    private static readonly Bet RouletteGreen = new (15, 17, "green");

    private readonly RouletteTable rouletteTable = new ();

    public GoofsinoModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.bot.CommandDictionary.TryAddCommand(new Command("red", this.RedCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("black", this.BlackCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("even", this.EvenCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("odd", this.OddCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("high", this.HighCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("low", this.LowCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("green", this.GreenCommand, unlisted: true));

        this.bot.CommandDictionary.TryAddCommand(new Command("declarebankruptcy", this.DeclareBankruptcyCommand));

        this.bot.CommandDictionary.TryAddCommand(new Command("spin", this.SpinCommand, CommandAccessibilityModifier.StreamerOnly));

        this.bot.CommandDictionary.TryAddCommand(new Command("creditscore", this.BankruptcyCountCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("balance", this.BalanceCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("gamboard", this.GambaPointLeaderboardCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("thehouse", this.HouseRevenueCommand));
    }

    public async Task InitializeAsync()
    {
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            try
            {
                await CreateTablesAsync(sqliteConnection);
                await transaction.CommitAsync();
            }
            catch (SqliteException e)
            {
                Console.WriteLine($"SQLITE EXCEPTION: {e}");
                await transaction.RollbackAsync();
            }
        }
    }

    private static async Task CreateTablesAsync(SqliteConnection sqliteConnection)
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

    private static async Task<long> GetBankruptcyCountAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT Count from Bankruptcies WHERE UserID = @UserID";
        sqliteCommand.Parameters.AddWithValue("@UserID", userID);

        return Convert.ToInt64(await sqliteCommand.ExecuteScalarAsync());
    }

    private static async Task AddUserToGambaPointsTableAsync(SqliteConnection sqliteConnection, string userID)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "INSERT INTO GambaPoints VALUES (@UserID, 1000, unixepoch('now','subsec')) ON CONFLICT DO NOTHING;";
        sqliteCommand.Parameters.AddWithValue("@UserID", long.Parse(userID));

        await sqliteCommand.ExecuteNonQueryAsync();
    }

    private static async Task<long> GetHouseBalance(SqliteConnection sqliteConnection)
    {
        return await GetBalanceAsync(sqliteConnection, TheHouseID);
    }

    private static async Task<List<UserNameAndCount>> GetTopGambaPointsUsersAsync(SqliteConnection sqliteConnection)
    {
        List<UserNameAndCount> leaderboardEntries = [];

        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText =
            @$"SELECT TwitchUsers.UserName, GambaPoints.Balance
                    FROM TwitchUsers
                    INNER JOIN GambaPoints ON TwitchUsers.UserID = GambaPoints.UserID
                    ORDER BY GambaPoints.Balance DESC, GambaPoints.LastUpdateTimestamp DESC 
                    LIMIT 5;
            ";

        using var reader = await sqliteCommand.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            leaderboardEntries.Add(new UserNameAndCount(reader.GetString(0), reader.GetInt64(1)));
        }

        return leaderboardEntries;
    }

    private static async Task<List<string>> ResolveAllBetsByTypeAsync(SqliteConnection sqliteConnection, Bet bet, bool success)
    {
        List<string> messages = [];

        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "SELECT Bets.UserID, TwitchUsers.UserName, Bets.Amount FROM Bets INNER JOIN TwitchUsers ON TwitchUsers.UserID = Bets.UserID WHERE BetTypeID = @BetTypeID;";
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);

        using var reader = await sqliteCommand.ExecuteReaderAsync();

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

            long balance = await GetBalanceAsync(sqliteConnection, userID);

            messages.Add($"{userName} {verb} {Math.Abs(amount)} gamba points for betting on {bet.BetName}. Balance: {balance + amount} gamba points");

            await AddBalanceAsync(sqliteConnection, userID, amount);

            amount *= -1;
            await AddBalanceAsync(sqliteConnection, TheHouseID, amount);
        }

        await DeleteAllBetsByTypeAsync(sqliteConnection, bet);

        return messages;
    }

    private static async Task ResetUserGambaPointsBalanceAndIncrementBankruptciesAsync(SqliteConnection sqliteConnection, string userID)
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
    }

    /*private static async Task<bool> ResolveBetAsync(SqliteConnection sqliteConnection, string userID, Bet bet, bool success)
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
    }*/

    private static async Task DeleteAllBetsByTypeAsync(SqliteConnection sqliteConnection, Bet bet)
    {
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "DELETE FROM Bets WHERE BetTypeID = @BetTypeID;";
        sqliteCommand.Parameters.AddWithValue("@BetTypeID", bet.TypeID);
        await sqliteCommand.ExecuteNonQueryAsync();
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
        sqliteCommand.CommandText = "DELETE FROM Bets WHERE UserID = @UserID AND BetTypeID = @BetTypeID;";
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

    private static async Task SetupUserIfNotSetUpAsync(SqliteConnection sqliteConnection, string userID, string userName)
    {
        await Bot.InsertOrUpdateTwitchUserAsync(sqliteConnection, userID, userName);
        await AddUserToGambaPointsTableAsync(sqliteConnection, userID);
    }

    private async Task HouseRevenueCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        long balance;
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        {
            balance = await GetHouseBalance(sqliteConnection);
        }

        this.bot.SendMessage($"The House has earned {balance} gamba points in revenue.", isReversed);
    }

    private async Task GambaPointLeaderboardCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        List<UserNameAndCount> leaderboardEntries = [];

        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        {
            leaderboardEntries = await GetTopGambaPointsUsersAsync(sqliteConnection);
        }

        string leaderboardString = Bot.GetLeaderboardString(leaderboardEntries);

        this.bot.SendMessage(leaderboardString, isReversed);
    }

    private async Task BankruptcyCountCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.DisplayName;

        long bankruptcyCount = 0;
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            await SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);
            bankruptcyCount = await GetBankruptcyCountAsync(sqliteConnection, userID);
        }

        string s = bankruptcyCount == 1 ? string.Empty : "s";
        this.bot.SendMessage($"{userName} has declared bankruptcy {bankruptcyCount} time{s}", isReversed);
    }

    private async Task BalanceCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.DisplayName;

        long balance;
        long totalBets;
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            try
            {
                await SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);
                balance = await GetBalanceAsync(sqliteConnection, userID);
                totalBets = await GetTotalBetsAsync(sqliteConnection, userID);

                await transaction.CommitAsync();

                if (totalBets > 0)
                {
                    this.bot.SendMessage($"@{userName} {balance} gamba points - {totalBets} in active bets = {balance - totalBets} points available", isReversed);
                }
                else
                {
                    this.bot.SendMessage($"@{userName} {balance} gamba points", isReversed);
                }
            }
            catch (SqliteException e)
            {
                this.bot.SendMessage("Hey, Goof, your bot broke! (Balance Command)", false);
                Console.WriteLine($"SQLITE EXCEPTION: {e}");
                await transaction.RollbackAsync();
            }
        }
    }

    private async Task DeclareBankruptcyCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.DisplayName;

        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            try
            {
                await SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);
                long balance = await GetBalanceAsync(sqliteConnection, userID);
                long totalBets = await GetTotalBetsAsync(sqliteConnection, userID);

                if (totalBets + balance <= 0)
                {
                    await ResetUserGambaPointsBalanceAndIncrementBankruptciesAsync(sqliteConnection, userID);
                    long count = await GetBankruptcyCountAsync(sqliteConnection, userID);

                    await transaction.CommitAsync();

                    string suffix = Program.GetSuffix(count);
                    this.bot.SendMessage($"{userName} has declared bankruptcy for the {count}{suffix} time. They now have 1000 Gamba Points", isReversed);

                    int timeoutDuration = Random.Shared.Next(151 + (int)count) + 30;
                    await this.bot.TimeoutUserAsync(userID, timeoutDuration);
                }
                else
                {
                    await transaction.CommitAsync();
                    this.bot.SendMessage("Come back when you're broke, bud", isReversed);
                }
            }
            catch (SqliteException e)
            {
                this.bot.SendMessage("Hey, Goof, your bot broke! (Declare Bankruptcy Command)", false);
                Console.WriteLine($"SQLITE EXCEPTION!!!\n{e.ToString()}");
                await transaction.RollbackAsync();
            }
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

    private async Task GreenCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, RouletteGreen);
    }

    private async Task EvenCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, RouletteEven);
    }

    private async Task OddCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, RouletteOdd);
    }

    private async Task HighCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, RouletteHigh);
    }

    private async Task LowCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, RouletteLow);
    }

    private async Task BetCommandHelperAsync(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs, Bet bet)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.DisplayName;

        bool withdraw = false;
        bool allIn = false;

        switch (commandArgs.ToLowerInvariant())
        {
            case "w":
                withdraw = true;
                break;
            case "withdraw":
                goto case "w";
            case "all":
                allIn = true;
                break;
            case "all in":
                goto case "all";
            case "allin":
                goto case "all";
            case "a":
                goto case "all";
        }


        if ((long.TryParse(commandArgs, out long amount) && amount >= RouletteMinimumBet) || withdraw || allIn)
        {
            using (var sqliteConnection = this.bot.OpenSqliteConnection())
            using (var transaction = sqliteConnection.BeginTransaction())
            using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
            {
                try
                {
                    await SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);

                    long existingBets;
                    string message;
                    if (withdraw)
                    {
                        existingBets = await GetBetAmountAsync(sqliteConnection, userID, bet);
                        await DeleteBetFromTableAsync(sqliteConnection, userID, bet);
                        await transaction.CommitAsync();

                        if (existingBets > 0)
                        {
                            message = $"{userName} withdrew their {existingBets} point bet on {bet.BetName}";
                        }
                        else
                        {
                            message = $"@{userName}, you haven't bet anything on {bet.BetName}";
                        }
                    }
                    else
                    {
                        existingBets = await GetBetAmountAsync(sqliteConnection, userID, bet);

                        if (allIn)
                        {
                            long balance = await GetBalanceAsync(sqliteConnection, userID);
                            amount = balance - existingBets;
                        }

                        bool success = await TryPlaceBetAsync(sqliteConnection, userID, bet, amount);
                        await transaction.CommitAsync();

                        if (success)
                        {
                            if (existingBets > 0)
                            {
                                message = $"{userName} bet {amount} more on {bet.BetName} for a total of {amount + existingBets}";
                            }
                            else
                            {
                                message = $"{userName} bet {amount} on {bet.BetName}";
                            }
                        }
                        else
                        {
                            message = $"@{userName}, you don't have that many points";
                        }
                    }

                    this.bot.SendMessage(message, isReversed);
                }
                catch (SqliteException e)
                {
                    this.bot.SendMessage("Hey, Goof, your bot broke! (Bet Command)", false);
                    Console.WriteLine($"SQLITE EXCEPTION: {e}");
                    await transaction.RollbackAsync();
                }
            }
        }
        else
        {
            this.bot.SendMessage($"Include an amount (minimum: {RouletteMinimumBet}), \"all\" to bet everything, or \"w\" to withdraw your bet", isReversed);
        }
    }

    private async Task SpinCommand(string commandArgs = "", bool isReversed = false, OnChatCommandReceivedArgs eventArgs = null)
    {
        this.rouletteTable.Spin();
        var color = this.rouletteTable.Color;

        this.bot.SendMessage($"The wheel landed on {this.rouletteTable.LastSpinResult} ({Enum.GetName(color)})", isReversed);

        List<string> messages = [];
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            try
            {
                if (color == RouletteTable.RouletteColor.Red)
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteRed, true));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteBlack, false));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteGreen, false));
                }
                else if (color == RouletteTable.RouletteColor.Black)
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteRed, false));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteBlack, true));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteGreen, false));
                }
                else
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteRed, false));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteBlack, false));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteGreen, true));
                }

                if (this.rouletteTable.Even)
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteEven, true));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteOdd, false));
                }
                else if (this.rouletteTable.Odd)
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteEven, false));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteOdd, true));
                }
                else
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteEven, false));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteOdd, false));
                }

                if (this.rouletteTable.High)
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteHigh, true));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteLow, false));
                }
                else if (this.rouletteTable.Low)
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteHigh, false));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteLow, true));
                }
                else
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteHigh, false));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteLow, false));
                }

                await transaction.CommitAsync();

                foreach (string message in messages)
                {
                    this.bot.SendMessage(message, isReversed);
                }
            }
            catch (SqliteException e)
            {
                this.bot.SendMessage("Hey, Goof, your bot broke! (Roulette Wheel Spin)", false);
                Console.WriteLine($"SQLITE EXCEPTION: {e}");
                await transaction.RollbackAsync();
            }
        }
    }
}
