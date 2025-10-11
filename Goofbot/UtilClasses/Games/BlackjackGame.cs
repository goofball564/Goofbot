namespace Goofbot.UtilClasses.Games;

using Goofbot.Modules;
using Goofbot.Structs;
using Goofbot.UtilClasses.Bets;
using Goofbot.UtilClasses.Cards;
using Goofbot.UtilClasses.Enums;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

internal class BlackjackGame
{
    public readonly BlockingCollection<BlackjackCommand> CommandQueue = new (new ConcurrentQueue<BlackjackCommand>());
    public readonly BlockingCollection<CancelableQueuedObject<UserIDAndName>> PlayerQueue = new (new ConcurrentQueue<CancelableQueuedObject<UserIDAndName>>());

    private const double BlackjackPayoutRatio = 1.5;
    private const int MaximumPlayerHandValue = 30;
    private const int MaximumDealerHandValue = 26;
    private const int MaximumPlayers = 5;

    private readonly ShoeOfPlayingCards cards;
    private readonly Bot bot;
    private readonly GoofsinoModule goofsino;

    private readonly bool hitOnSoft17;

    private readonly int totalCardsValue;
    private int remainingCardsValue = 0;

    private List<UserIDAndName> players;
    private bool canDouble;
    private bool canSplit;
    private int currentHandIndex;
    private int numBustsAndBlackjacks;
    private BlackjackCommand lastCommand;
    private bool lastCommandIsReversed;
    private List<BlackjackHand> playerHands;
    private BlackjackHand dealerHand;

    private CancellationTokenSource playerTimeoutCancellationTokenSource;
    private System.Timers.Timer playerTimeoutTimer;

    public BlackjackGame(GoofsinoModule goofsino, Bot bot, int numDecks = 1, int remainingCardsToRequireReshuffle = 26, bool hitOnSoft17 = false)
    {
        this.goofsino = goofsino;
        this.bot = bot;
        this.cards = new ShoeOfPlayingCards(numDecks, remainingCardsToRequireReshuffle);
        this.hitOnSoft17 = hitOnSoft17;

        foreach (PlayingCard card in this.cards)
        {
            this.totalCardsValue += BlackjackHand.CardValues[card.Rank];
        }

        this.playerTimeoutTimer = new (TimeSpan.FromSeconds(60));
        this.playerTimeoutTimer.Elapsed += this.TimerCallback;

        Task backgroundTask = Task.Run(async () =>
        {
            while (true)
            {
                this.ResetGame();
                await this.WaitForPlayersJoinAsync();
                await this.StartGameAsync();
                await this.PlayerPlayAsync();
                await this.DealerPlayAsync();
                await this.EndGameAsync();
            }
        });
    }

    private PlayingCard DealerFaceUpCard
    {
        get { return this.dealerHand[0]; }
    }

    private PlayingCard DealerHoleCard
    {
        get { return this.dealerHand[1]; }
    }

    private static string GetHandAndHandValueMessage(BlackjackHand hand)
    {
        return $"{GetHandMessage(hand)} {GetHandValueMessage(hand)}";
    }

    private static string GetHandMessage(BlackjackHand hand)
    {
        StringBuilder stringBuilder = new();
        string other = hand.Type == BlackjackHandType.Split ? " other" : string.Empty;
        stringBuilder.Append($"{hand.UserName}'s{other} hand: {hand}");

        return stringBuilder.ToString();
    }

    private static string GetHandValueMessage(BlackjackHand hand)
    {
        int handValue = hand.GetValue(out bool handIsSoft);
        string soft = handIsSoft ? "soft " : string.Empty;

        StringBuilder stringBuilder = new();
        stringBuilder.Append($"(Value: {soft}{handValue})");

        if (hand.HasBust())
        {
            stringBuilder.Append(". That's a bust!");
        }
        else if (hand.HasBlackjack())
        {
            stringBuilder.Append(". That's a blackjack!");
        }

        return stringBuilder.ToString();
    }

    private void TimerCallback(object o, ElapsedEventArgs e)
    {
        try
        {
            this.playerTimeoutCancellationTokenSource.Cancel();
        }
        catch
        {
        }
    }

    private void ResetGame()
    {
        this.players = [];
        this.canDouble = false;
        this.canSplit = false;
        this.lastCommandIsReversed = false;
        this.currentHandIndex = -1;
        this.numBustsAndBlackjacks = 0;

        this.playerHands = [];
        this.dealerHand = [];
    }

    private async Task WaitForPlayersJoinAsync()
    {
        var player = this.PlayerQueue.Take();
        this.players.Add(player.Value);

        await this.WaitWhileIgnoringAllCommandsAsync(10000);

        for (int i = 0; i < MaximumPlayers - 1; i++)
        {
            if (this.PlayerQueue.TryTake(out player))
            {
                this.players.Add(player.Value);
            }
            else
            {
                break;
            }
        }
    }

    private async Task StartGameAsync()
    {
        await this.WaitWhileIgnoringAllCommandsAsync(1000);

        int requiredValue = (this.players.Count * MaximumPlayerHandValue) + MaximumDealerHandValue;
        for (int i = 0; i < this.players.Count; i++)
        {
            PlayingCard p1 = this.cards.Peek(i);
            PlayingCard p2 = this.cards.Peek(i + this.players.Count + 1);

            if (p1 != null && p2 != null && p1.Rank == p2.Rank)
            {
                requiredValue += MaximumPlayerHandValue;
            }
        }

        if (this.remainingCardsValue < requiredValue)
        {
            this.remainingCardsValue = this.totalCardsValue;
            this.cards.Shuffle();
            this.bot.SendMessage("Reshuffling the deck...", this.lastCommandIsReversed);
            await this.WaitWhileIgnoringAllCommandsAsync(1000);
        }

        this.DealFirstCards();
    }

    private async Task PlayerPlayAsync()
    {
        await this.WaitWhileIgnoringAllCommandsAsync(1000);

        if (this.dealerHand.HasBlackjack())
        {
            this.bot.SendMessage($"{this.GetFaceUpCardMessage()}. {this.GetHoleCardMessage()}. {GetHandValueMessage(this.dealerHand)}", this.lastCommandIsReversed);

            foreach (var hand in this.playerHands)
            {
                await this.WaitWhileIgnoringAllCommandsAsync(1000);
                this.bot.SendMessage(GetHandAndHandValueMessage(hand), this.lastCommandIsReversed);
            }

            this.numBustsAndBlackjacks = int.MaxValue;
        }
        else
        {
            await this.MoveToNextHandAsync();

            this.playerTimeoutTimer.Start();

            while (this.currentHandIndex < this.playerHands.Count)
            {
                try
                {
                    this.lastCommand = this.CommandQueue.Take(this.playerTimeoutCancellationTokenSource.Token);
                    this.lastCommandIsReversed = this.lastCommand.IsReversed;
                }
                catch
                {
                    await this.MoveToNextHandAsync();
                    continue;
                }

                if (this.lastCommand.UserID.Equals(this.playerHands[this.currentHandIndex].UserID))
                {
                    this.playerTimeoutTimer.Stop();

                    switch (this.lastCommand.CommandType)
                    {
                        case BlackjackCommandType.Hit:
                            this.canDouble = false;
                            this.canSplit = false;

                            this.DealTo(this.playerHands[this.currentHandIndex]);
                            this.bot.SendMessage(this.GetUserPromptMessage(this.playerHands[this.currentHandIndex]), this.lastCommandIsReversed);

                            if (this.playerHands[this.currentHandIndex].HasBust())
                            {
                                this.numBustsAndBlackjacks++;
                                await this.MoveToNextHandAsync();
                            }
                            else if (!this.playerHands[this.currentHandIndex].CanHit())
                            {
                                await this.MoveToNextHandAsync();
                            }

                            break;

                        case BlackjackCommandType.Stay:
                            await this.MoveToNextHandAsync();
                            break;

                        case BlackjackCommandType.Double:
                            if (this.canDouble)
                            {
                                var bet = this.playerHands[this.currentHandIndex].Type == BlackjackHandType.Normal ? Goofsino.Blackjack : Goofsino.BlackjackSplit;
                                long amount = await this.goofsino.GetBetAmountAsyncFuckIDK(this.lastCommand.UserID, bet);
                                var outcome = await this.goofsino.BetCommandHelperAsync(amount.ToString(), this.lastCommandIsReversed, this.lastCommand.EventArgs, bet);
                                if (outcome == BetCommandOutcome.PlaceBet)
                                {
                                    this.DealTo(this.playerHands[this.currentHandIndex]);
                                    this.bot.SendMessage(GetHandAndHandValueMessage(this.playerHands[this.currentHandIndex]), this.lastCommandIsReversed);

                                    if (this.playerHands[this.currentHandIndex].HasBust())
                                    {
                                        this.numBustsAndBlackjacks++;
                                    }

                                    await this.MoveToNextHandAsync();
                                }
                            }

                            break;

                        case BlackjackCommandType.Split:
                            if (this.canSplit)
                            {
                                var outcome = await this.goofsino.BetCommandHelperAsync(this.lastCommand.CommandArgs, this.lastCommandIsReversed, this.lastCommand.EventArgs, Goofsino.BlackjackSplit, allowWithdraw: false);

                                if (outcome == BetCommandOutcome.PlaceBet)
                                {
                                    this.Split(this.currentHandIndex);
                                    this.canSplit = false;

                                    string rank = Enum.GetName(this.playerHands[this.currentHandIndex][0].Rank).ToLowerInvariant();
                                    this.bot.SendMessage($"{this.playerHands[this.currentHandIndex].UserName} splits their second {rank} off into a second hand", this.lastCommandIsReversed);

                                    await this.WaitWhileIgnoringAllCommandsAsync(1000);
                                    this.DealTo(this.playerHands[this.currentHandIndex]);
                                    this.bot.SendMessage(this.GetUserPromptMessage(this.playerHands[this.currentHandIndex]), this.lastCommandIsReversed);
                                }
                            }

                            break;
                    }

                    this.playerTimeoutTimer.Start();
                }
            }

            this.playerTimeoutTimer.Stop();
        }
    }

    private async Task DealerPlayAsync()
    {
        await this.WaitWhileIgnoringAllCommandsAsync(1000);

        this.lastCommandIsReversed = false;

        // Dealer would have already revealed they have a blackjack
        if (!this.dealerHand.HasBlackjack())
        {
            this.bot.SendMessage(this.GetFaceUpCardMessage(), this.lastCommandIsReversed);

            await this.WaitWhileIgnoringAllCommandsAsync(1000);
            this.bot.SendMessage($"{this.GetHoleCardMessage()} {GetHandValueMessage(this.dealerHand)}", this.lastCommandIsReversed);
        }

        // Dealer doesn't need to hit if every hand busted or is a blackjack
        if (this.numBustsAndBlackjacks < this.playerHands.Count)
        {
            int value;
            while ((value = this.dealerHand.GetValue(out bool soft)) < 17 || (value == 17 && soft && this.hitOnSoft17))
            {
                await this.WaitWhileIgnoringAllCommandsAsync(1000);
                this.bot.SendMessage(this.GetHitMessage(this.dealerHand), this.lastCommandIsReversed);
            }

            if (this.dealerHand.CanHit())
            {
                await this.WaitWhileIgnoringAllCommandsAsync(1000);
                this.bot.SendMessage("Dealer stays", this.lastCommandIsReversed);
            }
        }
    }

    private async Task EndGameAsync()
    {
        await this.WaitWhileIgnoringAllCommandsAsync(1000);

        int dealerValue = this.dealerHand.GetValue(out bool _);
        bool dealerBust = this.dealerHand.HasBust();
        bool dealerBlackjack = this.dealerHand.HasBlackjack();

        using (var sqliteConnection = this.bot.OpenSqliteConnection())
        using (var transaction = sqliteConnection.BeginTransaction())
        using (await this.bot.SqliteReaderWriterLock.WriteLockAsync())
        {
            try
            {
                List<string> messages = [];
                for (int i = 0; i < this.playerHands.Count; i++)
                {
                    var bet = this.playerHands[i].Type == BlackjackHandType.Normal ? Goofsino.Blackjack : Goofsino.BlackjackSplit;
                    string userID = this.playerHands[i].UserID;
                    string userName = this.playerHands[i].UserName;

                    if (this.playerHands[i].HasBlackjack())
                    {
                        bet = new BlackjackBet(bet.TypeID, BlackjackPayoutRatio, bet.BetName);

                        if (dealerBlackjack)
                        {
                            messages.Add($"It's a tie! Bet on this hand returned to {userName}");
                            await Goofsino.DeleteBetFromTableAsync(sqliteConnection, userID, bet);
                        }
                        else
                        {
                            messages.Add(await Goofsino.ResolveBetAsync(sqliteConnection, userID, bet, true));
                        }
                    }
                    else if (this.playerHands[i].HasBust())
                    {
                        messages.Add(await Goofsino.ResolveBetAsync(sqliteConnection, userID, bet, false));
                    }
                    else if (dealerBust)
                    {
                        messages.Add(await Goofsino.ResolveBetAsync(sqliteConnection, userID, bet, true));
                    }
                    else
                    {
                        int handValue = this.playerHands[i].GetValue(out bool _);
                        if (handValue > dealerValue)
                        {
                            messages.Add(await Goofsino.ResolveBetAsync(sqliteConnection, userID, bet, true));
                        }
                        else if (handValue == dealerValue)
                        {
                            messages.Add($"It's a tie! Bet on this hand returned to {userName}");
                            await Goofsino.DeleteBetFromTableAsync(sqliteConnection, userID, bet);
                        }
                        else
                        {
                            messages.Add(await Goofsino.ResolveBetAsync(sqliteConnection, userID, bet, false));
                        }
                    }
                }

                await transaction.CommitAsync();

                await this.goofsino.SendMessagesAsync(messages, this.lastCommandIsReversed);
            }
            catch (SqliteException e)
            {
                this.bot.SendMessage("Hey, Goof, your bot broke (Blackjack)", false);
                Console.WriteLine($"SQLITE EXCEPTION\n{e}");
                await transaction.RollbackAsync();
            }
        }
    }

    private async Task MoveToNextHandAsync()
    {
        this.currentHandIndex++;

        if (this.currentHandIndex < this.playerHands.Count)
        {
            await this.WaitWhileIgnoringAllCommandsAsync(1000);

            var hand = this.playerHands[this.currentHandIndex];

            string message;

            switch (hand.Type)
            {
                // New player
                case BlackjackHandType.Normal:
                    try
                    {
                        this.playerTimeoutCancellationTokenSource.Dispose();
                    }
                    catch
                    {
                    }

                    this.playerTimeoutCancellationTokenSource = new ();

                    this.lastCommandIsReversed = false;
                    this.canDouble = this.playerHands[this.currentHandIndex].HasTwoCards();
                    this.canSplit = this.playerHands[this.currentHandIndex].IsTwoMatchingRanks();
                    message = this.GetUserPromptMessage(this.playerHands[this.currentHandIndex]);
                    break;

                // Same player still
                case BlackjackHandType.Split:
                    this.DealTo(this.playerHands[this.currentHandIndex]);
                    this.canDouble = this.playerHands[this.currentHandIndex].HasTwoCards();
                    this.canSplit = false;
                    message = this.GetUserPromptMessage(this.playerHands[this.currentHandIndex]);
                    break;

                default:
                    message = "GOOF YOUR BOT BROKE";
                    break;
            }

            this.bot.SendMessage(message, this.lastCommandIsReversed);

            // Hands can't bust from two cards
            if (hand.HasBlackjack())
            {
                this.numBustsAndBlackjacks++;
            }

            // Not all 21s are blackjacks
            if (!hand.CanHit())
            {
                // RECURSION
                await this.MoveToNextHandAsync();
            }
        }
    }

    private void IgnoreAllCommands(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var command = this.CommandQueue.Take(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private string GetFaceUpCardMessage()
    {
        return $"Dealer's face-up card: {this.DealerFaceUpCard.ToShortformString()}";
    }

    private string GetHoleCardMessage()
    {
        return $"Dealer's hole card: {this.DealerHoleCard.ToShortformString()}";
    }

    private string GetUserPromptMessage(BlackjackHand hand)
    {
        StringBuilder stringBuilder = new ();
        stringBuilder.Append(GetHandAndHandValueMessage(hand));

        if (hand.CanHit())
        {
            stringBuilder.Append($". {this.GetFaceUpCardMessage()} | {this.GetCommandsMessage()}");
        }

        return stringBuilder.ToString();
    }

    private string GetCommandsMessage()
    {
        List<string> commands = ["!hit", "!stay"];
        if (this.canDouble)
        {
            commands.Add("!double");
        }

        if (this.canSplit)
        {
            commands.Add("!split");
        }

        StringBuilder stringBuilder = new ();
        stringBuilder.Append(string.Join(", ", commands.GetRange(0, commands.Count - 1)));
        stringBuilder.Append(" or ");
        stringBuilder.Append(commands[commands.Count - 1]);

        return stringBuilder.ToString();
    }

    private async Task WaitWhileIgnoringAllCommandsAsync(int delay)
    {
        using CancellationTokenSource cancellationTokenSource = new ();
        Task ignoreAllCommandsTask = Task.Run(() => this.IgnoreAllCommands(cancellationTokenSource.Token));
        await Task.Delay(delay);
        await cancellationTokenSource.CancelAsync();
        await ignoreAllCommandsTask;
    }

    private string GetHitMessage(BlackjackHand hand)
    {
        StringBuilder stringBuilder = new ();

        PlayingCard card = this.DealTo(hand);
        stringBuilder.Append($"{hand.UserName} hits: {card.ToShortformString()} ");
        stringBuilder.Append(GetHandValueMessage(hand));

        return stringBuilder.ToString();
    }

    private PlayingCard DealTo(BlackjackHand hand)
    {
        PlayingCard card = this.cards.GetNextCard();
        this.remainingCardsValue -= BlackjackHand.CardValues[card.Rank];
        hand.Add(card);
        return card;
    }

    private void DealFirstCards()
    {
        foreach (var player in this.players)
        {
            this.playerHands.Add(new BlackjackHand(player.UserID, player.UserName));
        }

        const int initialNumCardsDealt = 2;
        for (int i = 0; i < initialNumCardsDealt; i++)
        {
            foreach (var hand in this.playerHands)
            {
                this.DealTo(hand);
            }

            this.DealTo(this.dealerHand);
        }
    }

    private void Split(int handIndex)
    {
        BlackjackHand currentHand = this.playerHands[handIndex];

        PlayingCard card = currentHand.TakeSecondCard();

        // Create new hand next to this hand with the card
        this.playerHands.Insert(handIndex + 1, new BlackjackHand(currentHand.UserID, currentHand.UserName, BlackjackHandType.Split));
        this.playerHands[handIndex + 1].Add(card);
    }
}
