namespace Goofbot.UtilClasses.Games;

using Goofbot.UtilClasses.Cards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Goofbot.UtilClasses.Cards.PlayingCard;

internal class BaccaratGame
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
        { PlayingCardRank.Ten, 0 },
        { PlayingCardRank.Jack, 0 },
        { PlayingCardRank.Queen, 0 },
        { PlayingCardRank.King, 0 },
    };

    private static readonly List<PlayingCardRank> Case4Ranks 
        = [PlayingCardRank.Two, PlayingCardRank.Three, PlayingCardRank.Four, PlayingCardRank.Five, PlayingCardRank.Six, PlayingCardRank.Seven];

    private static readonly List<PlayingCardRank> Case5Ranks
        = [PlayingCardRank.Four, PlayingCardRank.Five, PlayingCardRank.Six, PlayingCardRank.Seven];

    private static readonly List<PlayingCardRank> Case6Ranks
        = [PlayingCardRank.Six, PlayingCardRank.Seven];

    private readonly ShoeOfPlayingCards cards = new (6, 7);

    private PlayingCard[] playerHand = new PlayingCard[3];
    private PlayingCard[] bankerHand = new PlayingCard[3];

    public BaccaratGame()
    {
    }

    public enum BaccaratOutcome
    {
        Player,
        Banker,
        Tie,
    }

    public PlayingCard PlayerFirstCard
    {
        get { return this.playerHand[0]; }
    }

    public PlayingCard PlayerSecondCard
    {
        get { return this.playerHand[1]; }
    }

    public PlayingCard PlayerThirdCard
    {
        get { return this.playerHand[2]; }
    }

    public PlayingCard BankerFirstCard
    {
        get { return this.bankerHand[0]; }
    }

    public PlayingCard BankerSecondCard
    {
        get { return this.bankerHand[1]; }
    }

    public PlayingCard BankerThirdCard
    {
        get { return this.bankerHand[2]; }
    }

    public bool ReshuffleRequired
    {
        get { return this.cards.ReshuffleRequired; }
    }

    public void ShuffleShoe()
    {
        this.cards.ShuffleDeck();
    }

    public BaccaratOutcome DetermineOutcome()
    {
        int playerHandValue = this.GetPlayerHandValue();
        int bankerHandValue = this.GetBankerHandValue();

        if (playerHandValue < bankerHandValue)
        {
            return BaccaratOutcome.Banker;
        }
        else if (playerHandValue > bankerHandValue)
        {
            return BaccaratOutcome.Player;
        }
        else
        {
            return BaccaratOutcome.Tie;
        }
    }

    public bool PlayerShouldDrawThirdCard()
    {
        int playerHandValue = this.GetPlayerHandValue();
        return !(playerHandValue == 6 || playerHandValue == 7);
    }

    public bool BankerShouldDrawThirdCard()
    {
        int bankerHandValue = this.GetBankerHandValue();

        if (this.PlayerThirdCard == null)
        {
            return bankerHandValue == 6 || bankerHandValue == 7;
        }
        else
        {
            switch (bankerHandValue)
            {
                case <= 2:
                    return true;
                case 3:
                    return this.PlayerThirdCard.Rank != PlayingCardRank.Eight;
                case 4:
                    return Case4Ranks.Contains(this.PlayerThirdCard.Rank);
                case 5:
                    return Case5Ranks.Contains(this.PlayerThirdCard.Rank);
                case 6:
                    return Case6Ranks.Contains(this.PlayerThirdCard.Rank);
                case 7:
                    return false;
                default:
                    return false;
            }
        }
    }

    public int GetPlayerHandValue()
    {
        return GetHandValueHelper(this.playerHand);
    }

    public int GetBankerHandValue()
    {
        return GetHandValueHelper(this.bankerHand);
    }

    public void DealFirstCards()
    {
        this.playerHand[0] = (PlayingCard)this.cards.GetNextCard();
        this.bankerHand[0] = (PlayingCard)this.cards.GetNextCard();
        this.playerHand[1] = (PlayingCard)this.cards.GetNextCard();
        this.bankerHand[1] = (PlayingCard)this.cards.GetNextCard();
    }

    public void DealThirdCardToPlayer()
    {
        this.playerHand[2] = (PlayingCard)this.cards.GetNextCard();
    }

    public void DealThirdCardToBanker()
    {
        this.bankerHand[2] = (PlayingCard)this.cards.GetNextCard();
    }


    public void ResetHands()
    {
        Array.Clear(this.playerHand, 0, this.playerHand.Length);
        Array.Clear(this.bankerHand, 0, this.bankerHand.Length);
    }

    public PlayingCard BurnCards(out int numBurned)
    {
        PlayingCard card = (PlayingCard)this.cards.GetNextCard();

        numBurned = CardValues[card.Rank];
        numBurned = numBurned == 0 ? 10 : numBurned;

        for (int i = 0; i < numBurned; i++)
        {
            this.cards.GetNextCard();
        }

        return card;
    }

    private static int GetHandValueHelper(PlayingCard[] hand)
    {
        int total = 0;
        foreach (var card in hand)
        {
            if (card != null)
            {
                total += CardValues[card.Rank];
            }
        }

        return total % 10;
    }
}
