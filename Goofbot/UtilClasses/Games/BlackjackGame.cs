namespace Goofbot.UtilClasses.Games;

using Goofbot.UtilClasses.Cards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Goofbot.UtilClasses.Cards.PlayingCard;
internal class BlackjackGame
{
    private readonly ShoeOfPlayingCards cards = new (1, 26);

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

    public void Hit()
    {
        PlayingCard card = this.cards.GetNextCard();
        this.PlayerHands[this.CurrentPlayerHandIndex].Add(card);
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

    public void Split()
    {
        // Take second card from current hand
        PlayingCard card = this.PlayerHands[this.CurrentPlayerHandIndex][1];
        this.PlayerHands[this.CurrentPlayerHandIndex].RemoveAt(1);

        // Create new hand next to this hand with the card
        this.PlayerHands.Insert(this.CurrentPlayerHandIndex + 1, []);
        this.PlayerHands[this.CurrentPlayerHandIndex + 1].Add(card);

        // Deal second card to current hand and continue
        this.PlayerHands[this.CurrentPlayerHandIndex].Add(this.cards.GetNextCard());
    }
}
