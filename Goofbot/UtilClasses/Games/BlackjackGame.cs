namespace Goofbot.UtilClasses.Games;

using Goofbot.UtilClasses.Cards;
using System.Collections.Generic;

internal class BlackjackGame
{
    public readonly int MaxPlayerHands;

    private readonly ShoeOfPlayingCards cards;

    public BlackjackGame(int numDecks = 1, int remainingCardsToRequireReshuffle = 26, int maxPlayerHands = 2)
    {
        this.cards = new ShoeOfPlayingCards(numDecks, remainingCardsToRequireReshuffle);
        this.MaxPlayerHands = maxPlayerHands;
    }

    public int CurrentPlayerHandIndex { get; private set; } = 0;

    public int NumPlayerHands
    {
        get { return this.PlayerHands.Count; }
    }

    public List<BlackjackHand> PlayerHands { get; private set; }

    public BlackjackHand DealerHand { get; private set;  }

    public bool ReshuffleRequired
    {
        get { return this.cards.ReshuffleRequired; }
    }

    public PlayingCard DealerFaceUpCard
    {
        get { return this.DealerHand[0]; }
    }

    public PlayingCard DealerHoleCard
    {
        get { return this.DealerHand[1]; }
    }

    public int GetPlayerHandValue(int index, out bool soft)
    {
        return this.PlayerHands[index].GetValue(out soft);
    }

    public int GetDealerHandValue(out bool soft)
    {
        return this.DealerHand.GetValue(out soft);
    }

    public void ResetHands()
    {
        this.PlayerHands = [];
        this.PlayerHands.Add([]);
        this.DealerHand = [];
    }

    public void Hit(int handIndex)
    {
        if (handIndex < this.NumPlayerHands)
        {
            PlayingCard card = this.cards.GetNextCard();
            this.PlayerHands[handIndex].Add(card);
        }
    }

    public void Shuffle()
    {
        this.cards.Shuffle();
    }

    public void DealFirstCards()
    {
        this.PlayerHands[0].Add(this.cards.GetNextCard());
        this.DealerHand.Add(this.cards.GetNextCard());
        this.PlayerHands[0].Add(this.cards.GetNextCard());
        this.DealerHand.Add(this.cards.GetNextCard());
    }

    public void Split(int handIndex)
    {
        // Take second card from current hand
        PlayingCard card = this.PlayerHands[handIndex][1];
        this.PlayerHands[handIndex].RemoveAt(1);

        // Create new hand next to this hand with the card
        this.PlayerHands.Insert(handIndex + 1, []);
        this.PlayerHands[handIndex + 1].Add(card);
    }

    public bool CanSplit(int handIndex)
    {
        if (handIndex >= this.NumPlayerHands)
        {
            return false;
        }
        else
        {
            var hand = this.PlayerHands[handIndex];
            return (this.NumPlayerHands < this.MaxPlayerHands) && (hand.Count == 2) && (hand[0].Rank == hand[1].Rank);
        }
    }

    public bool CanDouble(int handIndex)
    {
        if (handIndex >= this.NumPlayerHands)
        {
            return false;
        }
        else
        {
            var hand = this.PlayerHands[handIndex];
            return hand.Count == 2;
        }
    }

    public bool CanHit(int handIndex)
    {
        if (handIndex >= this.NumPlayerHands)
        {
            return false;
        }
        else
        {
            var hand = this.PlayerHands[handIndex];
            return hand.GetValue(out bool _) < 21;
        }
    }

    public bool DealerHasBlackjack()
    {
        return this.DealerHand.GetValue(out bool _) == 21;
    }

    public bool DealerHasBust()
    {
        return this.DealerHand.GetValue(out bool _) > 21;
    }

    public bool HasBlackjack(int handIndex)
    {
        var hand = this.PlayerHands[handIndex];
        return hand.GetValue(out bool _) == 21;
    }

    public bool HasBust(int handIndex)
    {
        var hand = this.PlayerHands[handIndex];
        return hand.GetValue(out bool _) > 21;
    }
}
