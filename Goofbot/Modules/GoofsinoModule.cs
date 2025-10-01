namespace Goofbot.Modules;

using Goofbot.Structs;
using Goofbot.UtilClasses;
using Goofbot.UtilClasses.Bets;
using Goofbot.UtilClasses.Cards;
using Goofbot.UtilClasses.Games;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.Charity.GetCharityCampaign;
using TwitchLib.Client.Events;
using static Goofbot.UtilClasses.Games.BaccaratGame;

internal class GoofsinoModule : GoofbotModule
{
    private const long RouletteMinimumBet = 1;
    private const long BaccaratMinimumBet = 100;
    private const long BlackjackMinimumBet = 2;
    private const string TheHouseID = "-1";

    private static readonly RouletteBet RouletteColumn1 = new (2, 2, "column 1");
    private static readonly RouletteBet RouletteColumn2 = new (3, 2, "column 2");
    private static readonly RouletteBet RouletteColumn3 = new (4, 2, "column 3");
    private static readonly RouletteBet RouletteDozen1 = new (5, 2, "first dozen");
    private static readonly RouletteBet RouletteDozen2 = new (6, 2, "second dozen");
    private static readonly RouletteBet RouletteDozen3 = new (7, 2, "third dozen");
    private static readonly RouletteBet RouletteHigh = new (8, 1, "high");
    private static readonly RouletteBet RouletteLow = new (9, 1, "low");
    private static readonly RouletteBet RouletteEven = new (10, 1, "even");
    private static readonly RouletteBet RouletteOdd = new (11, 1, "odd");
    private static readonly RouletteBet RouletteRed = new (12, 1, "red");
    private static readonly RouletteBet RouletteBlack = new (13, 1, "black");
    private static readonly RouletteBet RouletteTopLine = new (14, 6, "top line");
    private static readonly RouletteBet RouletteGreen = new (15, 17, "green");

    private static readonly BaccaratBet BaccaratPlayer = new (16, 1, "player");
    private static readonly BaccaratBet BaccaratBanker = new (17, 0.95, "banker");
    private static readonly BaccaratBet BaccaratTie = new (18, 8, "a tie");

    private static readonly BlackjackBet Blackjack = new (19, 1, "blackjack");
    private static readonly BlackjackBet BlackjackSplit = new (20, 1, "their second hand");

    private readonly GoofsinoGameBetsOpenStatus rouletteBetsOpenStatus = new ();
    private readonly GoofsinoGameBetsOpenStatus baccaratBetsOpenStatus = new ();

    private readonly RouletteTable rouletteTable = new ();
    private readonly BaccaratGame baccaratGame = new ();
    private readonly BlackjackGame blackjackGame = new ();

    private AsyncReaderWriterLock blackjackStateLock = new ();
    private string blackjackPlayerID = string.Empty;
    private bool canHit = false;
    private bool canStand = false;
    private bool canDouble = false;

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

        this.bot.CommandDictionary.TryAddCommand(new Command("player", this.PlayerCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("banker", this.BankerCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("tie", this.TieCommand, unlisted: true));

        this.bot.CommandDictionary.TryAddCommand(new Command("blackjack", this.BlackjackCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("hit", this.HitCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("stay", this.StayCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new Command("double", this.DoubleCommand, unlisted: true));

        this.bot.CommandDictionary.TryAddCommand(new Command("declarebankruptcy", this.DeclareBankruptcyCommand));

        this.bot.CommandDictionary.TryAddCommand(new Command("spin", this.SpinCommand, CommandAccessibilityModifier.StreamerOnly));
        this.bot.CommandDictionary.TryAddCommand(new Command("deal", this.DealCommand, CommandAccessibilityModifier.StreamerOnly));

        this.bot.CommandDictionary.TryAddCommand(new Command("creditscore", this.BankruptcyCountCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("balance", this.BalanceCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("gamboard", this.GambaPointLeaderboardCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("thehouse", this.HouseRevenueCommand));

        // player bets on blackjack - allowed if blackjackPlayerID isn't set - initialize state
        // player starts with option to !hit, !stand, !double, and possibly !split
        // can only split on their very first action, if ranks match - can't split anymore after splitting
        // can only double on first action for the hand, besides splitting at the start
        // if they bust, it automatically moves on

        // IF USER ISN'T PLAYING (bet places user in queue if they're not in the queue, updates their bet if they are in the queue)
    }

    private async Task BlackjackCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        using (await this.blackjackStateLock.WriteLockAsync())
        {
            if (this.blackjackPlayerID.Equals(string.Empty) && await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Blackjack))
            {
                this.blackjackPlayerID = eventArgs.Command.ChatMessage.UserId;
                this.canHit = false;
                this.canStand = false;
                this.canDouble = false;
            }
        }
    }

    public override async Task InitializeAsync()
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
                amount = Convert.ToInt64(Math.Floor(amount * bet.PayoutRatio));
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
                            ON CONFLICT(UserID) DO UPDATE SET Count = Count + 1, LastUpdateTimestamp = unixepoch('now','subsec');";
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

        this.bot.SendMessage($"The House has made {balance} gamba points off you suckers", isReversed);
    }

    private async Task GambaPointLeaderboardCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        List<UserNameAndCount> leaderboardEntries = [];

        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        {
            leaderboardEntries = await GetTopGambaPointsUsersAsync(sqliteConnection);
        }

        string leaderboardString = Bot.GetLeaderboardString(leaderboardEntries, "Gamba Point");

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

    private async Task PlayerCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, BaccaratPlayer);
    }

    private async Task BankerCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, BaccaratBanker);
    }

    private async Task TieCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, BaccaratTie);
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

    private async Task<bool> BetCommandHelperAsync(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs, Bet bet)
    {
        bool betPlaced = false;

        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.DisplayName;
        long minimumBet = 0;

        switch (bet)
        {
            case RouletteBet:
                minimumBet = RouletteMinimumBet;
                if (!await this.rouletteBetsOpenStatus.GetBetsOpenAsync())
                {
                    this.bot.SendMessage($"@{userName}, bets are closed on the Roulette table. Wait for them to open again.", isReversed);
                    return false;
                }

                break;

            case BaccaratBet:
                minimumBet = BaccaratMinimumBet;
                if (!await this.baccaratBetsOpenStatus.GetBetsOpenAsync())
                {
                    this.bot.SendMessage($"@{userName}, bets are closed on the Baccarat table. Wait for them to open again.", isReversed);
                    return false;
                }

                break;

            case BlackjackBet:
                minimumBet = BlackjackMinimumBet;
                break;
        }

        long amount = 0;
        bool amountProvided = false;
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

            case string providedAmount when long.TryParse(providedAmount, out amount):
                amountProvided = true;
                break;
        }

        if (amountProvided || withdraw || allIn)
        {
            using (var sqliteConnection = this.bot.OpenSqliteConnection())
            using (var transaction = sqliteConnection.BeginTransaction())
            using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
            {
                try
                {
                    await SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);

                    long existingBet;
                    string message;
                    if (withdraw)
                    {
                        existingBet = await GetBetAmountAsync(sqliteConnection, userID, bet);
                        await DeleteBetFromTableAsync(sqliteConnection, userID, bet);
                        await transaction.CommitAsync();

                        if (existingBet > 0)
                        {
                            message = $"{userName} withdrew their {existingBet} point bet on {bet.BetName}";
                        }
                        else
                        {
                            message = $"@{userName}, you haven't bet anything on {bet.BetName}";
                        }
                    }
                    else
                    {
                        if (allIn)
                        {
                            long balance = await GetBalanceAsync(sqliteConnection, userID);
                            long existingTotalBets = await GetTotalBetsAsync(sqliteConnection, userID);
                            amount = balance - existingTotalBets;
                        }

                        existingBet = await GetBetAmountAsync(sqliteConnection, userID, bet);
                        if (amount + existingBet >= minimumBet)
                        {
                            betPlaced = await TryPlaceBetAsync(sqliteConnection, userID, bet, amount);
                            await transaction.CommitAsync();

                            if (betPlaced)
                            {
                                if (existingBet > 0)
                                {
                                    message = $"{userName} bet {amount} more on {bet.BetName} for a total of {amount + existingBet}";
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
                        else
                        {
                            await transaction.CommitAsync();
                            message = $"@{userName} Minimum bet for this game: {minimumBet}";
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
            this.bot.SendMessage($"@{userName}, include an amount, \"withdraw\", or \"all in\"", isReversed);
        }

        return betPlaced;
    }

    private async Task DealCommand(string commandArgs = "", bool isReversed = false, OnChatCommandReceivedArgs eventArgs = null)
    {
        await this.baccaratBetsOpenStatus.SetBetsOpenAsync(false);
        await Task.Delay(1000);

        this.baccaratGame.ResetHands();

        if (this.baccaratGame.ReshuffleRequired)
        {
            this.baccaratGame.Shuffle();
            PlayingCard firstCard = this.baccaratGame.BurnCards(out int numCardsBurned);
            string s = numCardsBurned == 1 ? string.Empty : "s";
            this.bot.SendMessage(
                $"Reshuffling the cards... The dealer draws the {firstCard} and burns {numCardsBurned} card{s} face down. The dealer then deals two cards to the Banker and two cards to the Player",
                isReversed);
            await Task.Delay(8000);
        }
        else
        {
            this.bot.SendMessage("The dealer deals two cards to the Banker and two cards to the Player", isReversed);
            await Task.Delay(4000);
        }

        this.baccaratGame.DealFirstCards();
        int playerHandValue = this.baccaratGame.GetPlayerHandValue();
        int bankerHandValue = this.baccaratGame.GetBankerHandValue();

        string playerFirstCard = Enum.GetName(this.baccaratGame.PlayerFirstCard.Rank).ToLowerInvariant();
        string playerSecondCard = Enum.GetName(this.baccaratGame.PlayerSecondCard.Rank).ToLowerInvariant();

        string n = Program.StartsWithVowel(playerFirstCard) ? "n" : string.Empty;
        string n2 = Program.StartsWithVowel(playerSecondCard) ? "n" : string.Empty;

        this.bot.SendMessage(
            $"The Player's hand: a{n} {playerFirstCard} and a{n2} {playerSecondCard}. Value: {playerHandValue}",
            isReversed);
        await Task.Delay(4000);

        string bankerFirstCard = Enum.GetName(this.baccaratGame.BankerFirstCard.Rank).ToLowerInvariant();
        string bankerSecondCard = Enum.GetName(this.baccaratGame.BankerSecondCard.Rank).ToLowerInvariant();

        n = Program.StartsWithVowel(bankerFirstCard) ? "n" : string.Empty;
        n2 = Program.StartsWithVowel(bankerSecondCard) ? "n" : string.Empty;

        this.bot.SendMessage(
            $"The Banker's hand: a{n} {bankerFirstCard} and a{n2} {bankerSecondCard}. Value: {bankerHandValue}",
            isReversed);
        await Task.Delay(4000);

        BaccaratOutcome outcome;
        if (playerHandValue >= 8 || bankerHandValue >= 8)
        {
            outcome = this.baccaratGame.DetermineOutcome();
        }
        else
        {
            if (this.baccaratGame.PlayerShouldDrawThirdCard())
            {
                this.baccaratGame.DealThirdCardToPlayer();

                playerHandValue = this.baccaratGame.GetPlayerHandValue();
                string playerThirdCard = Enum.GetName(this.baccaratGame.PlayerThirdCard.Rank).ToLowerInvariant();
                n = Program.StartsWithVowel(playerThirdCard) ? "n" : string.Empty;

                this.bot.SendMessage($"The Player is dealt a third card: a{n} {playerThirdCard}. The final value of their hand: {playerHandValue}", isReversed);
            }
            else
            {
                this.bot.SendMessage($"The Player stands. The final value of their hand: {playerHandValue}", isReversed);
            }

            await Task.Delay(4000);

            if (this.baccaratGame.BankerShouldDrawThirdCard())
            {
                this.baccaratGame.DealThirdCardToBanker();
                bankerHandValue = this.baccaratGame.GetBankerHandValue();
                string bankerThirdCard = Enum.GetName(this.baccaratGame.BankerThirdCard.Rank).ToLowerInvariant();
                n = Program.StartsWithVowel(bankerThirdCard) ? "n" : string.Empty;
                this.bot.SendMessage($"The Banker is dealt a third card: a{n} {bankerThirdCard}. The final value of their hand: {bankerHandValue}", isReversed);
            }
            else
            {
                this.bot.SendMessage($"The Banker stands. The final value of their hand: {bankerHandValue}", isReversed);
            }

            await Task.Delay(4000);

            outcome = this.baccaratGame.DetermineOutcome();
        }

        if (outcome == BaccaratOutcome.Tie)
        {
            this.bot.SendMessage("It's a tie!", isReversed);
        }
        else
        {
            this.bot.SendMessage($"{Enum.GetName(outcome)} wins!", isReversed);
        }

        Task delayTask = Task.Delay(2000);

        List<string> messages = [];
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            try
            {
                switch (outcome)
                {
                    case BaccaratOutcome.Player:

                        messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, BaccaratPlayer, true));
                        messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, BaccaratBanker, false));
                        messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, BaccaratTie, false));
                        break;
                    case BaccaratOutcome.Banker:
                        messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, BaccaratPlayer, false));
                        messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, BaccaratBanker, true));
                        messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, BaccaratTie, false));
                        break;
                    case BaccaratOutcome.Tie:
                        await DeleteAllBetsByTypeAsync(sqliteConnection, BaccaratPlayer);
                        await DeleteAllBetsByTypeAsync(sqliteConnection, BaccaratBanker);
                        messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, BaccaratTie, true));
                        if (messages.Count == 0)
                        {
                            messages.Add("All bets have been returned to the players");
                        }
                        else
                        {
                            messages.Add("All other bets have been returned to the players");
                        }

                        break;
                }

                await transaction.CommitAsync();

                await delayTask;
                foreach (string message in messages)
                {
                    await delayTask;
                    this.bot.SendMessage(message, isReversed);
                    delayTask = Task.Delay(333);
                }

                await this.baccaratBetsOpenStatus.SetBetsOpenAsync(true);
            }
            catch (SqliteException e)
            {
                this.bot.SendMessage("Hey, Goof, your bot broke! (Baccarat)", false);
                Console.WriteLine($"SQLITE EXCEPTION: {e}");
                await transaction.RollbackAsync();
            }
        }


    }

    private async Task SpinCommand(string commandArgs = "", bool isReversed = false, OnChatCommandReceivedArgs eventArgs = null)
    {
        await this.rouletteBetsOpenStatus.SetBetsOpenAsync(false);

        this.bot.SendMessage("Spinning the wheel...", isReversed);
        await Task.Delay(2000);

        this.rouletteTable.Spin();
        var color = this.rouletteTable.Color;

        this.bot.SendMessage($"The wheel landed on {this.rouletteTable.LastSpinResult} ({Enum.GetName(color)})", isReversed);

        Task delayTask = Task.Delay(2000);

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

                await delayTask;
                foreach (string message in messages)
                {
                    await delayTask;
                    this.bot.SendMessage(message, isReversed);
                    delayTask = Task.Delay(333);
                }

                await this.rouletteBetsOpenStatus.SetBetsOpenAsync(true);
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
