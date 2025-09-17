namespace Goofbot.UtilClasses;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

internal class DeckOfCards
{
    private readonly List<Card> cards = [];
    private int currentIndex = 0;

    public DeckOfCards()
    {
        foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit)))
        {
            foreach (CardRank rank in Enum.GetValues(typeof(CardRank)))
            {
                this.cards.Add(new Card(suit, rank));
            }
        }
    }

    public enum CardSuit
    {
        Spades,
        Hearts,
        Diamonds,
        Clubs,
    }

    public enum CardRank
    {
        Ace,
        Two,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        Ten,
        Jack,
        Queen,
        King,
    }

    public void ShuffleDeck()
    {
        this.currentIndex = 0;

        // Knuth Shuffle
        for (int i = this.cards.Count - 1; i >= 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (this.cards[j], this.cards[i]) = (this.cards[i], this.cards[j]);
        }
    }

    public Card? GetNextCard()
    {
        if (this.currentIndex < this.cards.Count)
        {
            return this.cards[this.currentIndex++];
        }
        else
        {
            return null;
        }
    }

    public readonly struct Card(CardSuit suit, CardRank rank)
    {
        public readonly CardSuit Suit = suit;
        public readonly CardRank Rank = rank;
    }
}
