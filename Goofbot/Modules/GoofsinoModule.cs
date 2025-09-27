namespace Goofbot.Modules;

using Goofbot.Structs;
using Goofbot.UtilClasses;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client.Events;
using static Goofbot.UtilClasses.GoofsinoModuleHelperMethods;

internal class GoofsinoModule : GoofbotModule
{
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
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
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

    private async Task HouseRevenueCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        long balance;
        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        {
            balance = await GetHouseBalance(sqliteConnection);
        }

        this.bot.SendMessage($"The house has earned {balance} gamba points in revenue.", isReversed);
    }

    private async Task GambaPointLeaderboardCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        List<UserNameAndCount> leaderboardEntries = [];

        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        using (var sqliteCommand = sqliteConnection.CreateCommand())
        {
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
        }

        int i = 0;
        var stringBuilder = new StringBuilder();
        foreach (var user in leaderboardEntries)
        {
            string s = user.Count > 1 ? "s" : string.Empty;
            stringBuilder = stringBuilder.Append($"{i + 1}. {user.UserName} - {user.Count} Gamba Point{s}");
            if (i < leaderboardEntries.Count - 1)
            {
                stringBuilder = stringBuilder.Append(" | ");
            }

            i++;
        }

        this.bot.SendMessage(stringBuilder.ToString(), isReversed);
    }

    private async Task BankruptcyCountCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.Username;

        long bankruptcyCount = 0;
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
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
        string userName = eventArgs.Command.ChatMessage.Username;

        long balance;
        long totalBets;
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
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
        string userName = eventArgs.Command.ChatMessage.Username;

        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
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

                    string suffix = Program.GetSuffix(count);
                    this.bot.SendMessage($"{userName} has declared bankruptcy for the {count}{suffix} time. They now have 1000 Gamba Points", isReversed);

                    int timeoutDuration = Random.Shared.Next(151 + (int)count) + 30;
                    await this.bot.TimeoutUserAsync(userID, timeoutDuration);
                }
                else
                {
                    this.bot.SendMessage($"Come back when you're broke, bud", isReversed);
                }

                await transaction.CommitAsync();
            }
            catch (SqliteException e)
            {
                this.bot.SendMessage("Hey, Goof, your bot broke! (Declare Bankruptcy Command)", false);
                Console.WriteLine($"SQLITE EXCEPTION!!!\ne.ToString()");
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
        string userName = eventArgs.Command.ChatMessage.Username;

        bool withdraw = commandArgs.Equals("withdraw", StringComparison.OrdinalIgnoreCase) || commandArgs.Equals("w", StringComparison.OrdinalIgnoreCase);
        bool allIn = commandArgs.Equals("all", StringComparison.OrdinalIgnoreCase) || commandArgs.Equals("all in", StringComparison.OrdinalIgnoreCase);

        if ((long.TryParse(commandArgs, out long amount) && amount >= RouletteMinimumBet) || withdraw || allIn)
        {
            long existingBets = 0;
            using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
            using (var sqliteConnection = this.bot.OpenSqliteConnection())
            using (var transaction = sqliteConnection.BeginTransaction())
            {
                try
                {
                    await SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);

                    if (withdraw)
                    {
                        existingBets = await GetBetAmountAsync(sqliteConnection, userID, bet);
                        await DeleteBetFromTableAsync(sqliteConnection, userID, bet);
                        await transaction.CommitAsync();

                        if (existingBets > 0)
                        {
                            this.bot.SendMessage($"{userName} withdrew their {existingBets} point bet on {bet.BetName}", isReversed);
                        }
                    }
                    else if (allIn)
                    {
                        long balance = await GetBalanceAsync(sqliteConnection, userID);
                        existingBets = await GetBetAmountAsync(sqliteConnection, userID, bet);
                        amount = balance - existingBets;
                        bool success = await TryPlaceBetAsync(sqliteConnection, userID, bet, amount);
                        await transaction.CommitAsync();

                        if (success)
                        {
                            if (existingBets > 0)
                            {
                                this.bot.SendMessage($"{userName} bet {amount} more on {bet.BetName} for a total of {amount + existingBets}", isReversed);
                            }
                            else
                            {
                                this.bot.SendMessage($"{userName} bet {amount} on {bet.BetName}", isReversed);
                            }
                        }
                    }
                    else
                    {
                        existingBets = await GetBetAmountAsync(sqliteConnection, userID, bet);
                        bool success = await TryPlaceBetAsync(sqliteConnection, userID, bet, amount);
                        await transaction.CommitAsync();

                        if (success)
                        {
                            if (existingBets > 0)
                            {
                                this.bot.SendMessage($"{userName} bet {amount} more on {bet.BetName} for a total of {amount + existingBets}", isReversed);
                            }
                            else
                            {
                                this.bot.SendMessage($"{userName} bet {amount} on {bet.BetName}", isReversed);
                            }
                        }
                    }
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
            this.bot.SendMessage($"Minimum bet: {RouletteMinimumBet}", isReversed);
        }
    }

    private async Task SpinCommand(string commandArgs = "", bool isReversed = false, OnChatCommandReceivedArgs eventArgs = null)
    {
        this.rouletteTable.Spin();
        var color = this.rouletteTable.Color;

        this.bot.SendMessage($"The wheel landed on {this.rouletteTable.LastSpinResult} ({Enum.GetName(color)})", isReversed);

        List<string> messages = [];
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
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
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteBlack, true));
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

    private async Task<bool> TryDeclareBankruptcy(string userID)
    {
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        {
            try
            {
                var balanceTask = GetBalanceAsync(sqliteConnection, userID);
                var totalBetsTask = GetTotalBetsAsync(sqliteConnection, userID);

                long balance = await balanceTask;
                long totalBets = await totalBetsTask;

                if (totalBets + balance <= 0)
                {
                    await ResetUserGambaPointsBalanceAndIncrementBankruptciesAsync(sqliteConnection, userID);
                    await transaction.CommitAsync();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (SqliteException e)
            {

                await transaction.RollbackAsync();
                return false;
            }
        }
    }
}
