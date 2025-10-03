namespace Goofbot.UtilClasses.Games;

using Goofbot.Modules;
using Goofbot.UtilClasses.Cards;
using Goofbot.UtilClasses.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal class BlackjackGame
{
    public readonly BlockingCollection<BlackjackCommand> CommandQueue = new (new ConcurrentQueue<BlackjackCommand>());

    private readonly ShoeOfPlayingCards cards;
    private readonly Bot bot;
    private readonly GoofsinoModule goofsino;

    private readonly int maxPlayerHands;
    private readonly bool hitOnSoft17;

    private string currentPlayerUserID;
    private string currentPlayerUserName;
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

        Task backgroundTask = Task.Run(async () =>
        {
            while (true)
            {
                this.ResetGame();
                this.WaitForPlayerJoin();
                await this.StartGameAsync();
                this.PlayerPlay();
                await this.DealerPlayAsync();
                this.EndGame();
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
        this.currentPlayerUserID = string.Empty;
        this.currentPlayerUserName = string.Empty;
        this.canDouble = false;
        this.canSplit = false;
        this.currentHandIndex = 0;
        this.numBusts = 0;

        this.playerHands = [];
        this.playerHands.Add([]);
        this.dealerHand = [];
    }

    private void WaitForPlayerJoin()
    {
        while (true)
        {
            this.lastCommand = this.CommandQueue.Take();
            if (this.lastCommand.CommandType == BlackjackCommandType.Join)
            {
                this.currentPlayerUserID = this.lastCommand.UserID;
                this.currentPlayerUserName = this.lastCommand.UserName;

                bool withdraw = GoofsinoModule.WithdrawAliases.Contains(this.lastCommand.CommandArgs);
                bool success = true; // await this.BetCommandHelperAsync(command.Args, command.IsReversed, command.EventArgs, Blackjack);

                if (success && !withdraw)
                {
                    break;
                }
            }
        }
    }

    private async Task StartGameAsync()
    {
        if (this.cards.ReshuffleRequired)
        {
            this.cards.Shuffle();
            this.bot.SendMessage("Reshuffling the deck...", this.lastCommand.IsReversed);
            await this.WaitWhileIgnoringAllCommandsAsync(2000);
        }

        this.DealFirstCards();

        this.bot.SendMessage($"The dealer's face-up card: {this.DealerFaceUpCard.RankString()}", this.lastCommand.IsReversed);

        await this.WaitWhileIgnoringAllCommandsAsync(1000);
        this.AnnounceHand(this.playerHands[0], this.currentPlayerUserName);
    }

    private void PlayerPlay()
    {
        if (!this.dealerHand.HasBlackjack())
        {
            this.canDouble = this.playerHands[this.currentHandIndex].CanDouble();
            this.canSplit = this.playerHands[this.currentHandIndex].CanSplit();

            while (true)
            {
                this.lastCommand = this.CommandQueue.Take();
                if (this.lastCommand.UserID.Equals(this.currentPlayerUserID))
                {
                    switch (this.lastCommand.CommandType)
                    {
                        case BlackjackCommandType.Hit:
                            this.HitAndAnnounceStatus(this.playerHands[this.currentHandIndex], this.currentPlayerUserName);
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

                            break;

                        case BlackjackCommandType.Stay:
                            this.MoveToNextHand();
                            break;

                        case BlackjackCommandType.Double:
                            if (this.canDouble)
                            {
                                // long amount = await this.GetBetAmountAsyncFuckIDK(lastCommand.UserID, Blackjack);
                                // bool success = await this.BetCommandHelperAsync(amount.ToString(), lastCommand.IsReversed, lastCommand.EventArgs, Blackjack);
                                if (true)
                                {
                                    this.HitAndAnnounceStatus(this.playerHands[this.currentHandIndex], this.currentPlayerUserName);

                                    if (this.playerHands[this.currentHandIndex].HasBust())
                                    {
                                        this.numBusts++;
                                    }

                                    this.MoveToNextHand();
                                }
                            }

                            break;

                        case BlackjackCommandType.Split:
                            /*if (this.canSplit)
                            {
                                bool withdraw = GoofsinoModule.WithdrawAliases.Contains(this.lastCommand.CommandArgs);
                                bool success = true; // await this.BetCommandHelperAsync(lastCommand.CommandArgs, lastCommand.IsReversed, lastCommand.EventArgs, BlackjackSplit);

                                if (success && !withdraw)
                                {
                                    this.Split(this.currentHandIndex);
                                    this.canSplit = false;
                                    this.canDouble = false;

                                    this.bot.SendMessage("You split your cards into two hands", this.lastCommand.IsReversed);
                                }
                            }*/

                            break;
                    }
                }

                if (this.currentHandIndex >= this.playerHands.Count)
                {
                    break;
                }
            }
        }
    }

    private async Task DealerPlayAsync()
    {
        await this.WaitWhileIgnoringAllCommandsAsync(1000);

        int value = this.dealerHand.GetValue(out bool handIsSoft);
        string soft = handIsSoft ? "soft " : string.Empty;
        this.bot.SendMessage($"The dealer reveals their hole card: {this.DealerHoleCard.RankString()}. Value of their hand: {soft}{value}", this.lastCommand.IsReversed);

        if (this.numBusts < this.playerHands.Count)
        {
            while ((value = this.dealerHand.GetValue(out bool _)) < 17)
            {
                await this.WaitWhileIgnoringAllCommandsAsync(1000);
                this.HitAndAnnounceStatus(this.dealerHand, "The dealer");
            }
        }
    }

    private void EndGame()
    {
        int dealerValue = this.dealerHand.GetValue(out bool _);
        bool dealerBust = this.dealerHand.HasBust();

        for (int i = 0; i < this.playerHands.Count; i++)
        {
            var bet = i == 0 ? GoofsinoModule.Blackjack : GoofsinoModule.BlackjackSplit;
            if (this.playerHands[i].HasBust())
            {
                this.bot.SendMessage($"{this.currentPlayerUserName} loses :(", this.lastCommand.IsReversed);
            }
            else if (dealerBust)
            {
                this.bot.SendMessage($"{this.currentPlayerUserName} wins!", this.lastCommand.IsReversed);
            }
            else
            {
                int handValue = this.playerHands[i].GetValue(out bool _);
                if (handValue > dealerValue)
                {
                    this.bot.SendMessage($"{this.currentPlayerUserName} wins!", this.lastCommand.IsReversed);
                }
                else if (handValue == dealerValue)
                {
                    this.bot.SendMessage("It's a tie!", this.lastCommand.IsReversed);
                }
                else
                {
                    this.bot.SendMessage($"{this.currentPlayerUserName} loses :(", this.lastCommand.IsReversed);
                }
            }
        }
    }

    private void MoveToNextHand()
    {
        this.currentHandIndex++;

        if (this.currentHandIndex < this.playerHands.Count)
        {
            this.canDouble = this.playerHands[this.currentHandIndex].CanDouble();
            this.canSplit = this.CanSplit(this.playerHands[this.currentHandIndex]);
            this.HitAndAnnounceStatus(this.playerHands[this.currentHandIndex], $"{this.currentPlayerUserName}'s next hand");
        }
    }

    private bool CanSplit(BlackjackHand hand)
    {
        return hand.CanSplit() && this.playerHands.Count < this.maxPlayerHands;
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

    private void AnnounceHand(BlackjackHand hand, string userName)
    {
        int handValue = hand.GetValue(out bool handIsSoft);
        string soft = handIsSoft ? "soft " : string.Empty;

        StringBuilder stringBuilder = new ();
        stringBuilder.Append($"{userName}'s hand: {hand}. Value: {soft}{handValue}");

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

    private void HitAndAnnounceStatus(BlackjackHand hand, string userName)
    {
        StringBuilder stringBuilder = new ();

        PlayingCard card = this.Hit(hand);
        int handValue = hand.GetValue(out bool handIsSoft);
        string soft = handIsSoft ? "soft " : string.Empty;

        stringBuilder.Append($"{userName} is dealt {card.RankString()}. Value: {soft}{handValue}");

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

    private PlayingCard Hit(BlackjackHand hand)
    {
        PlayingCard card = this.cards.GetNextCard();
        hand.Add(card);
        return card;
    }

    private void DealFirstCards()
    {
        this.playerHands[0].Add(this.cards.GetNextCard());
        this.dealerHand.Add(this.cards.GetNextCard());
        this.playerHands[0].Add(this.cards.GetNextCard());
        this.dealerHand.Add(this.cards.GetNextCard());
    }

    private void Split(int handIndex)
    {
        // Take second card from current hand
        PlayingCard card = this.playerHands[handIndex][1];
        this.playerHands[handIndex].RemoveAt(1);

        // Create new hand next to this hand with the card
        this.playerHands.Insert(handIndex + 1, []);
        this.playerHands[handIndex + 1].Add(card);
    }
}
