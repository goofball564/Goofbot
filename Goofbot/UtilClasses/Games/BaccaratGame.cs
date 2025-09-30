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

    private PlayingCard[] puntoHand = new PlayingCard[3];
    private PlayingCard[] bancoHand = new PlayingCard[3];

    public BaccaratGame()
    {
    }

    public enum BaccaratOutcome
    {
        Punto,
        Banco,
        Tie,
    }

    public PlayingCard PuntoFirstCard
    {
        get { return this.puntoHand[0]; }
    }

    public PlayingCard PuntoSecondCard
    {
        get { return this.puntoHand[1]; }
    }

    public PlayingCard PuntoThirdCard
    {
        get { return this.puntoHand[2]; }
    }

    public PlayingCard BancoFirstCard
    {
        get { return this.bancoHand[0]; }
    }

    public PlayingCard BancoSecondCard
    {
        get { return this.bancoHand[1]; }
    }

    public PlayingCard BancoThirdCard
    {
        get { return this.bancoHand[2]; }
    }

    public BaccaratOutcome DetermineOutcome()
    {
        int puntoHandValue = this.GetPuntoHandValue();
        int bancoHandValue = this.GetBancoHandValue();

        if (puntoHandValue < bancoHandValue)
        {
            return BaccaratOutcome.Banco;
        }
        else if (puntoHandValue > bancoHandValue)
        {
            return BaccaratOutcome.Punto;
        }
        else
        {
            return BaccaratOutcome.Tie;
        }
    }

    public bool ShouldPuntoDrawThirdCard()
    {
        int puntoHandValue = this.GetPuntoHandValue();
        return puntoHandValue == 6 || puntoHandValue == 7;
    }

    public bool ShouldBancoDrawThirdCard()
    {
        int bancoHandValue = this.GetBancoHandValue();

        if (this.PuntoThirdCard == null)
        {
            return bancoHandValue == 6 || bancoHandValue == 7;
        }
        else
        {
            switch (bancoHandValue)
            {
                case <= 2:
                    return true;
                case 3:
                    return this.PuntoThirdCard.Rank != PlayingCardRank.Eight;
                case 4:
                    return Case4Ranks.Contains(this.PuntoThirdCard.Rank);
                case 5:
                    return Case5Ranks.Contains(this.PuntoThirdCard.Rank);
                case 6:
                    return Case6Ranks.Contains(this.PuntoThirdCard.Rank);
                case 7:
                    return false;
                default:
                    return false;
            }
        }
    }

    public int GetPuntoHandValue()
    {
        return GetHandValueHelper(this.puntoHand);
    }

    public int GetBancoHandValue()
    {
        return GetHandValueHelper(this.bancoHand);
    }

    public void DealFirstCards()
    {
        this.puntoHand[0] = (PlayingCard)this.cards.GetNextCard();
        this.bancoHand[0] = (PlayingCard)this.cards.GetNextCard();
        this.puntoHand[1] = (PlayingCard)this.cards.GetNextCard();
        this.bancoHand[1] = (PlayingCard)this.cards.GetNextCard();
    }

    public void ResetHands()
    {
        Array.Clear(this.puntoHand, 0, this.puntoHand.Length);
        Array.Clear(this.bancoHand, 0, this.bancoHand.Length);
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
