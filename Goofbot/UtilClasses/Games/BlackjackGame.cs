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

    private const double BlackjackPayoutRatio = 1.5;
    private const int MaximumPlayerHandValue = 30;
    private const int MaximumDealerHandValue = 26;

    private readonly ShoeOfPlayingCards cards;
    private readonly Bot bot;
    private readonly GoofsinoModule goofsino;

    private readonly int maxPlayerHands;
    private readonly bool hitOnSoft17;

    private readonly Task backgroundTask;

    private readonly int totalCardsValue;
    private int remainingCardsValue = 0;

    private List<UserIDAndName> players;
    private bool canDouble;
    private bool canSplit;
    private int currentHandIndex;
    private int numBusts;
    private BlackjackCommand lastCommand;
    private List<BlackjackHand> playerHands;
    private BlackjackHand dealerHand;

    public BlackjackGame(GoofsinoModule goofsino, Bot bot, int numDecks = 1, int remainingCardsToRequireReshuffle = 26, int maxPlayerHands = 2, bool hitOnSoft17 = false)
    {
        this.goofsino = goofsino;
        this.bot = bot;
        this.cards = new ShoeOfPlayingCards(numDecks, remainingCardsToRequireReshuffle);
        this.maxPlayerHands = maxPlayerHands;
        this.hitOnSoft17 = hitOnSoft17;

        foreach (PlayingCard card in this.cards)
        {
            this.totalCardsValue += BlackjackHand.CardValues[card.Rank];
        }

        this.backgroundTask = Task.Run(async () =>
        {
            while (true)
            {
                this.ResetGame();
                await this.WaitForPlayerJoin();
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

    private void ResetGame()
    {
        this.players = [];
        this.canDouble = false;
        this.canSplit = false;
        this.currentHandIndex = -1;
        this.numBusts = 0;

        this.playerHands = [];
        this.dealerHand = [];
    }

    private async Task WaitForPlayerJoin()
    {
        while (true)
        {
            this.lastCommand = this.CommandQueue.Take();
            if (this.lastCommand.CommandType == BlackjackCommandType.Join)
            {
                this.players.Add(new UserIDAndName(this.lastCommand.UserID, this.lastCommand.UserName));

                bool withdraw = GoofsinoModule.WithdrawAliases.Contains(this.lastCommand.CommandArgs);
                bool success = await this.goofsino.BetCommandHelperAsync(this.lastCommand.CommandArgs, this.lastCommand.IsReversed, this.lastCommand.EventArgs, Goofsino.Blackjack);

                if (success && !withdraw)
                {
                    await this.WaitWhileIgnoringAllCommandsAsync(1000);
                    break;
                }
            }
        }
    }

    private async Task StartGameAsync()
    {
        PlayingCard p1 = this.cards.Peek(0);
        PlayingCard p2 = this.cards.Peek(2);

        bool firstPlayerWillBeAbleToSplit = p1 != null && p2 != null & p1.Rank == p2.Rank;
        int requiredValue = (MaximumPlayerHandValue * this.maxPlayerHands) + MaximumDealerHandValue;
        if (firstPlayerWillBeAbleToSplit)
        {
            requiredValue -= MaximumPlayerHandValue;
        }

        if (this.remainingCardsValue < requiredValue)
        {
            this.remainingCardsValue = this.totalCardsValue;
            this.cards.Shuffle();
            this.bot.SendMessage("Reshuffling the deck...", this.lastCommand.IsReversed);
            await this.WaitWhileIgnoringAllCommandsAsync(2000);
        }

        this.DealFirstCards();
    }

    private async Task PlayerPlayAsync()
    {
        if (this.dealerHand.HasBlackjack())
        {
            this.bot.SendMessage($"The dealer's face-up card: {this.DealerFaceUpCard.RankString()}. The dealer reveals their hole card: {this.DealerHoleCard.RankString()}. That's a blackjack!", this.lastCommand.IsReversed);
            await this.WaitWhileIgnoringAllCommandsAsync(1000);
            this.MoveToNextHand();
        }
        else
        {
            this.bot.SendMessage($"The dealer's face-up card: {this.DealerFaceUpCard.RankString()}", this.lastCommand.IsReversed);
            await this.WaitWhileIgnoringAllCommandsAsync(1000);
            this.MoveToNextHand();

            // Sets player timeout for Blackjack.
            // Game will proceed without player input automatically.
            // Timeout is reset when the player enters a command
            using var timeoutTokenSource = new CancellationTokenSource();
            using var timer = new System.Timers.Timer(TimeSpan.FromSeconds(60));
            timer.Elapsed += (o, e) => timeoutTokenSource.Cancel();
            timer.Start();

            while (!timeoutTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    this.lastCommand = this.CommandQueue.Take(timeoutTokenSource.Token);
                    if (this.lastCommand.UserID.Equals(this.playerHands[this.currentHandIndex].UserID))
                    {
                        switch (this.lastCommand.CommandType)
                        {
                            case BlackjackCommandType.Hit:
                                timer.Stop();

                                this.HitAndAnnounceStatus(this.playerHands[this.currentHandIndex]);
                                this.canDouble = false;
                                this.canSplit = false;

                                if (this.playerHands[this.currentHandIndex].HasBust())
                                {
                                    this.numBusts++;
                                    this.MoveToNextHand();
                                }
                                else if (this.playerHands[this.currentHandIndex].HasBlackjack())
                                {
                                    this.MoveToNextHand();
                                }

                                timer.Start();

                                break;

                            case BlackjackCommandType.Stay:
                                timer.Stop();
                                this.MoveToNextHand();
                                timer.Start();
                                break;

                            case BlackjackCommandType.Double:
                                timer.Stop();
                                if (this.canDouble)
                                {
                                    long amount = await this.goofsino.GetBetAmountAsyncFuckIDK(this.lastCommand.UserID, Goofsino.Blackjack);
                                    bool success = await this.goofsino.BetCommandHelperAsync(amount.ToString(), this.lastCommand.IsReversed, this.lastCommand.EventArgs, Goofsino.Blackjack);
                                    if (success)
                                    {
                                        this.HitAndAnnounceStatus(this.playerHands[this.currentHandIndex]);

                                        if (this.playerHands[this.currentHandIndex].HasBust())
                                        {
                                            this.numBusts++;
                                        }

                                        this.MoveToNextHand();
                                    }
                                }

                                timer.Start();

                                break;

                            case BlackjackCommandType.Split:
                                timer.Stop();
                                if (this.canSplit)
                                {
                                    bool withdraw = GoofsinoModule.WithdrawAliases.Contains(this.lastCommand.CommandArgs);
                                    bool success = await this.goofsino.BetCommandHelperAsync(this.lastCommand.CommandArgs, this.lastCommand.IsReversed, this.lastCommand.EventArgs, Goofsino.BlackjackSplit);

                                    if (success && !withdraw)
                                    {
                                        this.Split(this.currentHandIndex);
                                        this.canSplit = false;

                                        string rank = Enum.GetName(this.playerHands[this.currentHandIndex][0].Rank).ToLowerInvariant();

                                        await this.WaitWhileIgnoringAllCommandsAsync(1000);
                                        this.bot.SendMessage($"{this.playerHands[this.currentHandIndex].UserName} splits their second {rank} off into a second hand", this.lastCommand.IsReversed);

                                        await this.WaitWhileIgnoringAllCommandsAsync(2000);
                                        this.HitAndAnnounceStatus(this.playerHands[this.currentHandIndex]);
                                    }
                                }

                                timer.Start();

                                break;
                        }
                    }

                    if (this.currentHandIndex >= this.playerHands.Count)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }

    private async Task DealerPlayAsync()
    {
        // Dealer would have already revealed they have a blackjack, and they don't need to play if they do
        if (!this.dealerHand.HasBlackjack())
        {
            int value = this.dealerHand.GetValue(out bool handIsSoft);
            string soft = handIsSoft ? "soft " : string.Empty;
            this.bot.SendMessage($"The dealer reveals their hole card: {this.DealerHoleCard.RankString()}. Value of their hand: {soft}{value}", this.lastCommand.IsReversed);
        }

        // Dealer doesn't need to hit if every hand busted
        if (this.numBusts < this.playerHands.Count)
        {
            int value;
            while ((value = this.dealerHand.GetValue(out bool soft)) < 17 || (value == 17 && soft && this.hitOnSoft17))
            {
                await this.WaitWhileIgnoringAllCommandsAsync(1000);
                this.HitAndAnnounceStatus(this.dealerHand);
            }
        }

        await this.WaitWhileIgnoringAllCommandsAsync(1000);
    }

    private async Task EndGameAsync()
    {
        int dealerValue = this.dealerHand.GetValue(out bool _);
        bool dealerBust = this.dealerHand.HasBust();

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
                    }

                    if (this.playerHands[i].HasBust())
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

                await this.goofsino.SendMessagesAsync(messages, this.lastCommand.IsReversed);
            }
            catch (SqliteException e)
            {
                this.bot.SendMessage("Hey, Goof, your bot broke (Blackjack)", false);
                Console.WriteLine($"SQLITE EXCEPTION\n{e}");
                await transaction.RollbackAsync();
            }
        }
    }

    private void MoveToNextHand()
    {
        this.currentHandIndex++;

        if (this.currentHandIndex < this.playerHands.Count)
        {
            var hand = this.playerHands[this.currentHandIndex];

            if (hand.Type == BlackjackHandType.Normal)
            {
                this.AnnounceHand(this.playerHands[this.currentHandIndex]);
                this.canDouble = this.playerHands[this.currentHandIndex].HandHasTwoCards();
                this.canSplit = this.playerHands[this.currentHandIndex].HandIsTwoMatchingRanks();

            }
            else if (hand.Type == BlackjackHandType.Split)
            {
                this.HitAndAnnounceStatus(this.playerHands[this.currentHandIndex]);
                this.canDouble = this.playerHands[this.currentHandIndex].HandHasTwoCards();
                this.canSplit = false;
            }

            if (hand.HasBlackjack())
            {
                // RECURSION
                this.MoveToNextHand();
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

    private void AnnounceHand(BlackjackHand hand)
    {
        int handValue = hand.GetValue(out bool handIsSoft);
        string soft = handIsSoft ? "soft " : string.Empty;

        StringBuilder stringBuilder = new ();
        stringBuilder.Append($"{hand.UserName}'s hand: {hand}. Value: {soft}{handValue}");

        if (hand.HasBust())
        {
            stringBuilder.Append(". That's a bust!");
        }
        else if (hand.HasBlackjack())
        {
            stringBuilder.Append(". That's a blackjack!");
        }

        this.bot.SendMessage(stringBuilder.ToString(), this.lastCommand.IsReversed);
    }

    private void HitAndAnnounceStatus(BlackjackHand hand)
    {
        StringBuilder stringBuilder = new ();

        PlayingCard card = this.DealTo(hand);
        int handValue = hand.GetValue(out bool handIsSoft);
        string soft = handIsSoft ? "soft " : string.Empty;

        stringBuilder.Append($"{hand.UserName} is dealt {card.RankString()}. Value: {soft}{handValue}");

        if (hand.HasBust())
        {
            stringBuilder.Append(". That's a bust!");
        }
        else if (hand.HasBlackjack())
        {
            stringBuilder.Append(". That's a blackjack!");
        }

        this.bot.SendMessage(stringBuilder.ToString(), this.lastCommand.IsReversed);
    }

    private async Task WaitWhileIgnoringAllCommandsAsync(int delay)
    {
        using CancellationTokenSource cancellationTokenSource = new ();
        Task ignoreAllCommandsTask = Task.Run(() => this.IgnoreAllCommands(cancellationTokenSource.Token));
        await Task.Delay(delay);
        await cancellationTokenSource.CancelAsync();
        await ignoreAllCommandsTask;
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

        if (this.playerHands.Count > 1)
        {
            foreach (var hand in this.playerHands)
            {
                this.AnnounceHand(hand);
            }
        }
    }

    private void Split(int handIndex)
    {
        BlackjackHand existingHand = this.playerHands[handIndex];

        // Take second card from current hand
        PlayingCard card = existingHand[1];
        existingHand.RemoveAt(1);

        // Create new hand next to this hand with the card
        this.playerHands.Insert(handIndex + 1, new BlackjackHand(existingHand.UserID, existingHand.UserName, BlackjackHandType.Split));
        this.playerHands[handIndex + 1].Add(card);
    }
}

// Blackjack command
// If not playing and not in queue, place the bet and join the queue
// If in queue, modify bet or leave queue
// If playing, can't use this command, can only play

// In queue:
// When play begins, dequeue up to MaxPlayers players, add to game

// Queue
// UserID
// IsCancelled

// Queue operations that modify elements or dequeue should use a lock I guess
// adding to queue corresponds to placing bet
// removing from queue corresponds to withdrawing bet

// Game
// Game has player hands
// List of players? (has count, naturally)
// at start of game after list of players is created, create original hands for each player, deal in order
// Play in order with similar rules as now

// Hands
// UserID
// original hand or split hand
