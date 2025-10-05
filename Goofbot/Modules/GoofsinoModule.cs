namespace Goofbot.Modules;

using Goofbot.Structs;
using Goofbot.UtilClasses;
using Goofbot.UtilClasses.Bets;
using Goofbot.UtilClasses.Cards;
using Goofbot.UtilClasses.Enums;
using Goofbot.UtilClasses.Games;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class GoofsinoModule : GoofbotModule
{
    public static readonly List<string> WithdrawAliases = ["withdraw", "w"];
    public static readonly List<string> AllInAliases = ["all in", "all", "allin", "a"];

    private const long RouletteMinimumBet = 1;
    private const long BaccaratMinimumBet = 100;
    private const long BlackjackMinimumBet = 0;

    private readonly GoofsinoGameBetsOpenStatus rouletteBetsOpenStatus = new ();
    private readonly GoofsinoGameBetsOpenStatus baccaratBetsOpenStatus = new ();

    private readonly RouletteTable rouletteTable = new ();
    private readonly BaccaratGame baccaratGame = new ();
    private readonly BlackjackGame blackjackGame;

    public GoofsinoModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.blackjackGame = new (this, bot, hitOnSoft17: true);

        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("red", this.RedCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("black", this.BlackCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("even", this.EvenCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("odd", this.OddCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("high", this.HighCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("low", this.LowCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("green", this.GreenCommand, unlisted: true));

        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("player", this.PlayerCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("banker", this.BankerCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("tie", this.TieCommand, unlisted: true));

        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("blackjack", this.BlackjackCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("hit", this.HitCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("stay", this.StayCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("double", this.DoubleCommand, unlisted: true));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("split", this.SplitCommand, unlisted: true));

        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("declarebankruptcy", this.DeclareBankruptcyCommand));

        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("spin", this.SpinCommand, CommandAccessibilityModifier.StreamerOnly));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("deal", this.DealCommand, CommandAccessibilityModifier.StreamerOnly));

        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("creditscore", this.BankruptcyCountCommand));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("balance", this.BalanceCommand));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("gamboard", this.GambaPointLeaderboardCommand));
        this.bot.CommandDictionary.TryAddCommand(new ChatCommand("thehouse", this.HouseRevenueCommand));
    }

    public override async Task InitializeAsync()
    {
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            try
            {
                await Goofsino.CreateTablesAsync(sqliteConnection);
                await transaction.CommitAsync();
            }
            catch (SqliteException e)
            {
                Console.WriteLine($"SQLITE EXCEPTION: {e}");
                await transaction.RollbackAsync();
            }
        }
    }

    public async Task<long> GetBetAmountAsyncFuckIDK(string userID, Bet bet)
    {
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        {
            return await Goofsino.GetBetAmountAsync(sqliteConnection, userID, bet);
        }
    }

    public async Task<bool> BetCommandHelperAsync(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs, Bet bet)
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

        bool withdraw = WithdrawAliases.Contains(commandArgs);
        bool allIn = AllInAliases.Contains(commandArgs);
        bool amountProvided = long.TryParse(commandArgs, out long amount);

        if (amountProvided || withdraw || allIn)
        {
            using (var sqliteConnection = this.bot.OpenSqliteConnection())
            using (var transaction = sqliteConnection.BeginTransaction())
            using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
            {
                try
                {
                    await Goofsino.SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);

                    long existingBet;
                    string message;
                    if (withdraw)
                    {
                        existingBet = await Goofsino.GetBetAmountAsync(sqliteConnection, userID, bet);
                        await Goofsino.DeleteBetFromTableAsync(sqliteConnection, userID, bet);

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
                            long balance = await Goofsino.GetBalanceAsync(sqliteConnection, userID);
                            long existingTotalBets = await Goofsino.GetTotalBetsAsync(sqliteConnection, userID);
                            amount = balance - existingTotalBets;
                        }

                        existingBet = await Goofsino.GetBetAmountAsync(sqliteConnection, userID, bet);
                        if (amount + existingBet >= minimumBet)
                        {
                            betPlaced = await Goofsino.TryPlaceBetAsync(sqliteConnection, userID, bet, amount);

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
                            message = $"@{userName} Minimum bet for this game: {minimumBet}";
                        }
                    }

                    await transaction.CommitAsync();
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

    private async Task HouseRevenueCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        long balance;
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        {
            balance = await Goofsino.GetHouseBalance(sqliteConnection);
        }

        this.bot.SendMessage($"The House has made {balance} gamba points off you suckers", isReversed);
    }

    private async Task GambaPointLeaderboardCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        List<UserNameAndCount> leaderboardEntries = [];

        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (await this.bot.SqliteReaderWriterLock.ReadLockAsync())
        {
            leaderboardEntries = await Goofsino.GetTopGambaPointsUsersAsync(sqliteConnection);
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
            await Goofsino.SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);
            bankruptcyCount = await Goofsino.GetBankruptcyCountAsync(sqliteConnection, userID);
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
                await Goofsino.SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);
                balance = await Goofsino.GetBalanceAsync(sqliteConnection, userID);
                totalBets = await Goofsino.GetTotalBetsAsync(sqliteConnection, userID);

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
                await Goofsino.SetupUserIfNotSetUpAsync(sqliteConnection, userID, userName);
                long balance = await Goofsino.GetBalanceAsync(sqliteConnection, userID);
                long totalBets = await Goofsino.GetTotalBetsAsync(sqliteConnection, userID);

                if (totalBets + balance <= 0)
                {
                    await Goofsino.ResetUserGambaPointsBalanceAndIncrementBankruptciesAsync(sqliteConnection, userID);
                    long count = await Goofsino.GetBankruptcyCountAsync(sqliteConnection, userID);

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
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Goofsino.BaccaratPlayer);
    }

    private async Task BankerCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Goofsino.BaccaratBanker);
    }

    private async Task TieCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Goofsino.BaccaratTie);
    }

    private async Task RedCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Goofsino.RouletteRed);
    }

    private async Task BlackCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Goofsino.RouletteBlack);
    }

    private async Task GreenCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Goofsino.RouletteGreen);
    }

    private async Task EvenCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Goofsino.RouletteEven);
    }

    private async Task OddCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Goofsino.RouletteOdd);
    }

    private async Task HighCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Goofsino.RouletteHigh);
    }

    private async Task LowCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        await this.BetCommandHelperAsync(commandArgs, isReversed, eventArgs, Goofsino.RouletteLow);
    }

    private async Task BlackjackCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        this.blackjackGame.CommandQueue.Add(new BlackjackCommand(BlackjackCommandType.Join, commandArgs, isReversed, eventArgs));
    }

    private async Task HitCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        this.blackjackGame.CommandQueue.Add(new BlackjackCommand(BlackjackCommandType.Hit, commandArgs, isReversed, eventArgs));
    }

    private async Task StayCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        this.blackjackGame.CommandQueue.Add(new BlackjackCommand(BlackjackCommandType.Stay, commandArgs, isReversed, eventArgs));
    }

    private async Task DoubleCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        this.blackjackGame.CommandQueue.Add(new BlackjackCommand(BlackjackCommandType.Double, commandArgs, isReversed, eventArgs));
    }

    private async Task SplitCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        this.blackjackGame.CommandQueue.Add(new BlackjackCommand(BlackjackCommandType.Split, commandArgs, isReversed, eventArgs));
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

                        messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.BaccaratPlayer, true));
                        messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.BaccaratBanker, false));
                        messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.BaccaratTie, false));
                        break;
                    case BaccaratOutcome.Banker:
                        messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.BaccaratPlayer, false));
                        messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.BaccaratBanker, true));
                        messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.BaccaratTie, false));
                        break;
                    case BaccaratOutcome.Tie:
                        await Goofsino.DeleteAllBetsByTypeAsync(sqliteConnection, Goofsino.BaccaratPlayer);
                        await Goofsino.DeleteAllBetsByTypeAsync(sqliteConnection, Goofsino.BaccaratBanker);
                        messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.BaccaratTie, true));
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
                await this.SendMessagesAsync(messages, isReversed);
            }
            catch (SqliteException e)
            {
                this.bot.SendMessage("Hey, Goof, your bot broke! (Baccarat)", false);
                Console.WriteLine($"SQLITE EXCEPTION: {e}");
                await transaction.RollbackAsync();
            }
        }

        await this.baccaratBetsOpenStatus.SetBetsOpenAsync(true);
    }

    public async Task SendMessagesAsync(List<string> messages, bool isReversed)
    {
        Task delayTask = Task.Delay(0);
        foreach (string message in messages)
        {
            await delayTask;
            this.bot.SendMessage(message, isReversed);
            delayTask = Task.Delay(500);
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
        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            try
            {
                List<string> messages = [];
                bool redSuccess = color == RouletteTable.RouletteColor.Red;
                bool blackSuccess = color == RouletteTable.RouletteColor.Black;
                bool greenSuccess = color == RouletteTable.RouletteColor.Green;

                messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.RouletteRed, redSuccess));
                messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.RouletteBlack, blackSuccess));
                messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.RouletteGreen, greenSuccess));

                messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.RouletteEven, this.rouletteTable.Even));
                messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.RouletteOdd, this.rouletteTable.Odd));

                messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.RouletteHigh, this.rouletteTable.High));
                messages.AddRange(await Goofsino.ResolveAllBetsByTypeAsync(sqliteConnection, Goofsino.RouletteLow, this.rouletteTable.Low));

                await transaction.CommitAsync();

                await delayTask;

                await this.SendMessagesAsync(messages, isReversed);
            }
            catch (SqliteException e)
            {
                this.bot.SendMessage("Hey, Goof, your bot broke! (Roulette Wheel Spin)", false);
                Console.WriteLine($"SQLITE EXCEPTION: {e}");
                await transaction.RollbackAsync();
            }
        }

        await this.rouletteBetsOpenStatus.SetBetsOpenAsync(true);
    }
}
