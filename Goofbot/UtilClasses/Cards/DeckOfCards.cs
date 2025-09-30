namespace Goofbot.UtilClasses.Cards;

using System.Collections.Generic;
using System.Security.Cryptography;

internal abstract class DeckOfCards
{
    protected readonly List<Card> cards = [];
    private int currentIndex = 0;

    public int Count
    {
        get
        {
            return this.cards.Count;
        }
    }

    public int Remaining
    {
        get
        {
            return this.cards.Count - this.currentIndex;
        }
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

    public Card Peek(int index)
    {
        return this.cards[index];
    }
}
