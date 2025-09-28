namespace Goofbot.UtilClasses.Cards;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

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
                cards.Add(new Card(suit, rank));
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

    public int Count
    {
        get
        {
            return cards.Count;
        }
    }

    public void ShuffleDeck()
    {
        currentIndex = 0;

        // Knuth Shuffle
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (cards[j], cards[i]) = (cards[i], cards[j]);
        }
    }

    public Card? GetNextCard()
    {
        if (currentIndex < cards.Count)
        {
            return cards[currentIndex++];
        }
        else
        {
            return null;
        }
    }

    public Card Peek(int index)
    {
        return cards[index];
    }

    public readonly struct Card(CardSuit suit, CardRank rank)
    {
        public readonly CardSuit Suit = suit;
        public readonly CardRank Rank = rank;

        public override string ToString()
        {
            return $"{Enum.GetName(Rank)} of {Enum.GetName(Suit)}";
        }
    }
}
