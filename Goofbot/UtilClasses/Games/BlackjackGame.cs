namespace Goofbot.UtilClasses.Games;

using Goofbot.Modules;
using Goofbot.Structs;
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

internal class BlackjackGame : IDisposable
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
    private readonly System.Timers.Timer playerTimeoutTimer;

    private readonly bool hitOnSoft17;

    private readonly int totalCardsValue;
    private int remainingCardsValue = 0;

    private List<UserIDAndName> players;
    private bool canDouble;
    private bool canSplit;
    private bool canSurrender;
    private int currentHandIndex;
    private int numBustsBlackjacksAndSurrenders;
    private BlackjackCommand lastCommand;
    private bool lastCommandIsReversed;
    private List<BlackjackHand> playerHands;
    private BlackjackHand dealerHand;

    private CancellationTokenSource playerTimeoutCancellationTokenSource;

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
                await this.ResolveBets();
            }
        });
    }

    public void Dispose()
    {
        try
        {
            this.playerTimeoutCancellationTokenSource.Dispose();
        }
        catch
        {
        }

        this.playerTimeoutTimer.Dispose();
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
        string other = hand.Type == BlackjackHandType.Split ? " other" : string.Empty;
        return $"{hand.UserName}'s{other} hand: {hand}";
    }

    private static string GetHandValueMessage(BlackjackHand hand)
    {
        int handValue = hand.GetValue(out bool handIsSoft);
        string soft = handIsSoft ? "soft " : string.Empty;

        StringBuilder stringBuilder = new ();
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
        this.canSurrender = false;
        this.lastCommandIsReversed = false;
        this.currentHandIndex = -1;
        this.numBustsBlackjacksAndSurrenders = 0;

        this.playerHands = [];
        this.dealerHand = [];
    }

    private async Task WaitForPlayersJoinAsync()
    {
        var player = this.PlayerQueue.Take();
        this.players.Add(player.Value);

        await this.WaitWhileRejectingAllCommandsAsync(10000);

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
        await this.WaitWhileRejectingAllCommandsAsync(1000);

        // Whether the bot shuffles before a round is based on if there's enough cards remaining in the shoe for this round.
        // Calculated based on values of cards, not count of cards (and whether players will have an option to split)
        // (assumes a maximum of one split per player in current implementation)
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
            this.cards.Shuffle();
            this.remainingCardsValue = this.totalCardsValue;
            this.bot.SendMessage("Shuffling the deck...", this.lastCommandIsReversed);
            await this.WaitWhileRejectingAllCommandsAsync(1000);
        }

        this.DealFirstCards();
    }

    private async Task PlayerPlayAsync()
    {
        await this.WaitWhileRejectingAllCommandsAsync(1000);

        if (this.dealerHand.HasBlackjack())
        {
            // Reveal all hands and move on without play
            this.bot.SendMessage($"{this.GetFaceUpCardMessage()}. {this.GetHoleCardMessage()}. {GetHandValueMessage(this.dealerHand)}", this.lastCommandIsReversed);

            foreach (var hand in this.playerHands)
            {
                await this.WaitWhileRejectingAllCommandsAsync(1000);
                this.bot.SendMessage(GetHandAndHandValueMessage(hand), this.lastCommandIsReversed);
            }
        }
        else
        {
            // We start at hand index -1 and move to hand index 0 to kick off the game
            await this.MoveToNextHandAsync();

            // Start the timer before play, or the first user may never time out :)
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
                    // If waiting for a command is cancelled by timing out,
                    // MoveToNextHandAsync resets timeout only if moving to the next player's hand,
                    // so if current player times out and has multiple hands, this may happen
                    // more than once
                    await this.MoveToNextHandAsync();
                    continue;
                }

                // Only take action on commands submitted by the current player
                if (this.lastCommand.UserID.Equals(this.playerHands[this.currentHandIndex].UserID))
                {
                    // Each command from the current player resets their timeout timer
                    this.playerTimeoutTimer.Stop();

                    var currentHand = this.playerHands[this.currentHandIndex];

                    switch (this.lastCommand.CommandType)
                    {
                        case BlackjackCommandType.Hit:
                            this.canDouble = false;
                            this.canSplit = false;
                            this.canSurrender = false;

                            this.DealTo(currentHand);
                            this.bot.SendMessage(this.GetUserPromptMessage(currentHand), this.lastCommandIsReversed);

                            if (currentHand.HasBust())
                            {
                                this.numBustsBlackjacksAndSurrenders++;
                            }

                            if (!currentHand.CanHit())
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
                                var bet = currentHand.Type == BlackjackHandType.Normal ? Goofsino.Blackjack : Goofsino.BlackjackSplit;
                                long amount = await this.goofsino.GetBetAmountAsyncFuckIDK(this.lastCommand.UserID, bet);
                                var outcome = await this.goofsino.BetCommandHelperAsync(amount.ToString(), this.lastCommandIsReversed, this.lastCommand.EventArgs, bet, allowWithdraw: false);

                                if (outcome == BetCommandOutcome.PlaceBet)
                                {
                                    this.DealTo(currentHand);
                                    this.bot.SendMessage(GetHandAndHandValueMessage(currentHand), this.lastCommandIsReversed);

                                    if (currentHand.HasBust())
                                    {
                                        this.numBustsBlackjacksAndSurrenders++;
                                    }

                                    await this.MoveToNextHandAsync();
                                }
                            }

                            break;

                        case BlackjackCommandType.Split:
                            if (this.canSplit)
                            {
                                long amount = await this.goofsino.GetBetAmountAsyncFuckIDK(this.lastCommand.UserID, Goofsino.Blackjack);
                                var outcome = await this.goofsino.BetCommandHelperAsync(amount.ToString(), this.lastCommandIsReversed, this.lastCommand.EventArgs, Goofsino.BlackjackSplit, allowWithdraw: false, allowAdd: false);

                                if (outcome == BetCommandOutcome.PlaceBet)
                                {
                                    this.Split(this.currentHandIndex);
                                    this.canSplit = false;
                                    this.canSurrender = false;

                                    await this.WaitWhileRejectingAllCommandsAsync(1000);
                                    string rank = Enum.GetName(currentHand[0].Rank).ToLowerInvariant();
                                    this.bot.SendMessage($"{currentHand.UserName} splits their second {rank} off into a second hand", this.lastCommandIsReversed);

                                    await this.WaitWhileRejectingAllCommandsAsync(1000);
                                    this.DealTo(currentHand);
                                    this.bot.SendMessage(this.GetUserPromptMessage(currentHand), this.lastCommandIsReversed);
                                }
                            }

                            break;

                        case BlackjackCommandType.Surrender:
                            if (this.canSurrender)
                            {
                                this.numBustsBlackjacksAndSurrenders++;
                                currentHand.Surrender();
                                await this.MoveToNextHandAsync();
                            }

                            break;
                    }

                    this.playerTimeoutTimer.Start();
                }
            }

            // Stop the timer after play, or this round's timer could affect the next round :)
            this.playerTimeoutTimer.Stop();
        }
    }

    private async Task DealerPlayAsync()
    {
        await this.WaitWhileRejectingAllCommandsAsync(1000);

        this.lastCommandIsReversed = false;

        // Dealer would have already revealed they have a blackjack
        if (!this.dealerHand.HasBlackjack())
        {
            this.bot.SendMessage(this.GetFaceUpCardMessage(), this.lastCommandIsReversed);

            await this.WaitWhileRejectingAllCommandsAsync(1000);
            this.bot.SendMessage($"{this.GetHoleCardMessage()} {GetHandValueMessage(this.dealerHand)}", this.lastCommandIsReversed);
        }

        // Dealer doesn't need to hit if every player hand busted, has a blackjack, or surrendered
        if (this.numBustsBlackjacksAndSurrenders < this.playerHands.Count)
        {
            int value;
            while ((value = this.dealerHand.GetValue(out bool soft)) < 17 || (value == 17 && soft && this.hitOnSoft17))
            {
                await this.WaitWhileRejectingAllCommandsAsync(1000);
                this.bot.SendMessage(this.GetHitMessage(this.dealerHand), this.lastCommandIsReversed);
            }

            if (this.dealerHand.CanHit())
            {
                await this.WaitWhileRejectingAllCommandsAsync(1000);
                this.bot.SendMessage("Dealer stays", this.lastCommandIsReversed);
            }
        }
    }

    private async Task ResolveBets()
    {
        await this.WaitWhileRejectingAllCommandsAsync(1000);

        int dealerValue = this.dealerHand.GetValue(out _);
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
                        if (dealerBlackjack)
                        {
                            messages.Add($"Bet on this hand returned to {userName}");
                            await Goofsino.DeleteBetFromTableAsync(sqliteConnection, userID, bet);
                        }
                        else
                        {
                            messages.Add(await Goofsino.ResolveBetAsync(sqliteConnection, userID, bet, true, payoutMultiplier: BlackjackPayoutRatio));
                        }
                    }
                    else if (this.playerHands[i].HasSurrendered)
                    {
                        messages.Add(await Goofsino.ResolveBetAsync(sqliteConnection, userID, bet, false, payoutMultiplier: 0.5));
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
                        int handValue = this.playerHands[i].GetValue(out _);
                        if (handValue > dealerValue)
                        {
                            messages.Add(await Goofsino.ResolveBetAsync(sqliteConnection, userID, bet, true));
                        }
                        else if (handValue == dealerValue)
                        {
                            messages.Add($"Bet on this hand returned to {userName}");
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
            await this.WaitWhileRejectingAllCommandsAsync(1000);

            var currentHand = this.playerHands[this.currentHandIndex];

            string message;

            switch (currentHand.Type)
            {
                // New player
                case BlackjackHandType.Normal:
                    // Reset timeout for new player
                    try
                    {
                        this.playerTimeoutCancellationTokenSource.Dispose();
                    }
                    catch
                    {
                    }
                    finally
                    {
                        this.playerTimeoutCancellationTokenSource = new ();
                    }

                    // Stop reversing messages for new player
                    this.lastCommandIsReversed = false;

                    // User prompt depends on this.canDouble, this.canSplit, and this.canSurrender's values, so set first
                    this.canDouble = true;
                    this.canSplit = this.playerHands[this.currentHandIndex].IsTwoMatchingRanks();
                    this.canSurrender = true;
                    message = this.GetUserPromptMessage(this.playerHands[this.currentHandIndex]);
                    break;

                // Same player still
                case BlackjackHandType.Split:
                    // Deal second card; split hands start with just one card from the original hand
                    this.DealTo(this.playerHands[this.currentHandIndex]);

                    this.canDouble = true;
                    this.canSplit = false;
                    this.canSurrender = false;
                    message = this.GetUserPromptMessage(this.playerHands[this.currentHandIndex]);
                    break;

                default:
                    message = "GOOF YOUR BOT BROKE";
                    break;
            }

            this.bot.SendMessage(message, this.lastCommandIsReversed);

            // Hands can't bust from two cards
            if (currentHand.HasBlackjack())
            {
                this.numBustsBlackjacksAndSurrenders++;
            }

            // Not all 21s are blackjacks
            if (!currentHand.CanHit())
            {
                // RECURSION
                await this.MoveToNextHandAsync();
            }
        }
    }

    private void RejectAllCommands(CancellationToken cancellationToken)
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

        if (this.canSurrender)
        {
            commands.Add("!surrender");
        }

        StringBuilder stringBuilder = new ();
        stringBuilder.Append(string.Join(", ", commands.GetRange(0, commands.Count - 1)));
        stringBuilder.Append(" or ");
        stringBuilder.Append(commands[commands.Count - 1]);

        return stringBuilder.ToString();
    }

    private async Task WaitWhileRejectingAllCommandsAsync(int delay)
    {
        using CancellationTokenSource cancellationTokenSource = new ();
        Task ignoreAllCommandsTask = Task.Run(() => this.RejectAllCommands(cancellationTokenSource.Token));
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

        // Take second card sets an internal value used in calculating if a hand is a blackjack
        // So should not manually remove card with other means :(
        PlayingCard card = currentHand.TakeSecondCard();

        // Create new hand next to this hand with the card
        this.playerHands.Insert(handIndex + 1, new BlackjackHand(currentHand.UserID, currentHand.UserName, BlackjackHandType.Split));
        this.playerHands[handIndex + 1].Add(card);
    }
}
