namespace Goofbot.UtilClasses.Games;

using Goofbot.UtilClasses.Cards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Goofbot.UtilClasses.Cards.PlayingCard;
internal class BlackjackGame
{
    private static readonly Dictionary<PlayingCardRank, int> CardValues = new ()
    {
        { PlayingCardRank.Ace, 1 },
        { PlayingCardRank.Two, 2 },
        { PlayingCardRank.Three, 3 },
        { PlayingCardRank.Four, 4 },
        { PlayingCardRank.Five, 5 },
        { PlayingCardRank.Six, 6 },
        { PlayingCardRank.Seven, 7 },
        { PlayingCardRank.Eight, 8 },
        { PlayingCardRank.Nine, 9 },
        { PlayingCardRank.Ten, 10 },
        { PlayingCardRank.Jack, 10 },
        { PlayingCardRank.Queen, 10 },
        { PlayingCardRank.King, 10 },
    };

    private readonly ShoeOfPlayingCards cards = new (1, 26);

    public List<PlayingCard> PlayerHand1 { get; private set; }

    public List<PlayingCard> PlayerHand2 { get; private set; }

    public List<PlayingCard> DealerHand { get; private set;  }

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

    public bool HasSplit { get; private set; }

    public int GetPlayerHand1Value(out bool soft)
    {
        return GetHandValueHelper(this.PlayerHand1, out soft);
    }

    public int GetPlayerHand2Value(out bool soft)
    {
        return GetHandValueHelper(this.PlayerHand2, out soft);
    }

    public int GetDealerHandValue(out bool soft)
    {
        return GetHandValueHelper(this.DealerHand, out soft);
    }

    public void ResetHands()
    {
        this.HasSplit = false;
        this.PlayerHand1 = [];
        this.PlayerHand2 = [];
        this.DealerHand = [];
    }

    public void Hit(bool secondHand)
    {
        PlayingCard card = (PlayingCard)this.cards.GetNextCard();
        if (secondHand)
        {
            this.PlayerHand2.Add(card);
        }
        else
        {
            this.PlayerHand1.Add(card);
        }
    }

    public void DealSecondCardToSecondHand()
    {
        this.PlayerHand2.Add((PlayingCard)this.cards.GetNextCard());
    }

    public void Shuffle()
    {
        this.cards.Shuffle();
    }

    public void DealFirstCards()
    {
        this.PlayerHand1.Add((PlayingCard)this.cards.GetNextCard());
        this.DealerHand.Add((PlayingCard)this.cards.GetNextCard());
        this.PlayerHand1.Add((PlayingCard)this.cards.GetNextCard());
        this.DealerHand.Add((PlayingCard)this.cards.GetNextCard());
    }

    public void Split()
    {
        PlayingCard card = this.PlayerHand1[1];
        this.PlayerHand1.RemoveAt(1);
        this.PlayerHand2.Add(card);
        this.PlayerHand1.Add((PlayingCard)this.cards.GetNextCard());
        this.HasSplit = true;
    }

    private static int GetHandValueHelper(List<PlayingCard> hand, out bool soft)
    {
        soft = false;

        int value = 0;
        foreach (PlayingCard card in hand)
        {
            value += CardValues[card.Rank];
        }

        // there can only be one ace valued 11 in a hand (11 * 2 > 21)
        if (hand.Any(c => c.Rank == PlayingCardRank.Ace) && value + 10 <= 21)
        {
            value += 10;
            soft = true;
        }

        return value;
    }
}
