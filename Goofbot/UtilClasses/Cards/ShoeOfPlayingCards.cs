namespace Goofbot.UtilClasses.Cards;

using System;
using static Goofbot.UtilClasses.Cards.PlayingCard;

internal class ShoeOfPlayingCards : DeckOfCards
{
    public ShoeOfPlayingCards(int numDecks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numDecks, 1);

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
}
