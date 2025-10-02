namespace Goofbot.UtilClasses.Cards;

using Goofbot.UtilClasses.Enums;
using System;

internal class ShoeOfPlayingCards : DeckOfCards<PlayingCard>
{
    protected readonly int remainingCardsToRequireReshuffle;

    public ShoeOfPlayingCards(int numDecks, int remainingCardsToRequireReshuffle)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numDecks, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(remainingCardsToRequireReshuffle, 0);

        this.remainingCardsToRequireReshuffle = remainingCardsToRequireReshuffle;

        for (int i = 0; i < numDecks; i++)
        {
            foreach (PlayingCardSuit suit in Enum.GetValues(typeof(PlayingCardSuit)))
            {
                foreach (PlayingCardRank rank in Enum.GetValues(typeof(PlayingCardRank)))
                {
                    this.cards.Add(new PlayingCard(suit, rank));
                }
            }
        }
    }

    public bool ReshuffleRequired
    {
        get
        {
            return this.Remaining <= this.remainingCardsToRequireReshuffle || this.Remaining == this.Count;
        }
    }
}
