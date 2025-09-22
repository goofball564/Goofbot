namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
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

    private async Task BalanceCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string userID = eventArgs.Command.ChatMessage.UserId;
        string userName = eventArgs.Command.ChatMessage.Username;

        long balance;
        long totalBets;
        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
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

        if (long.TryParse(commandArgs, out long amount) && amount >= RouletteMinimumBet)
        {
            long existingBets = 0;
            using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
            using (var sqliteConnection = this.bot.OpenSqliteConnection())
            using (var transaction = sqliteConnection.BeginTransaction())
            {
                try
                {
                    await SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);
                    existingBets = await GetBetAmountAsync(sqliteConnection, userID, bet);
                    await TryPlaceBetAsync(sqliteConnection, userID, bet, amount);

                    await transaction.CommitAsync();
                }
                catch (SqliteException e)
                {
                    this.bot.SendMessage("Hey, Goof, your bot broke! (Bet Command)", false);
                    Console.WriteLine($"SQLITE EXCEPTION: {e}");
                    await transaction.RollbackAsync();
                }
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
                }
                else if (color == RouletteTable.RouletteColor.Black)
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteRed, false));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteBlack, true));
                }
                else
                {
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteRed, false));
                    messages.AddRange(await ResolveAllBetsByTypeAsync(sqliteConnection, RouletteBlack, false));
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
}
