namespace Goofbot.UtilClasses.Cards;

using System.Collections.Generic;
using System.Security.Cryptography;

internal abstract class DeckOfCards<T>
    where T : Card
{
    protected readonly List<T> cards = [];
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

    public void Shuffle()
    {
        this.currentIndex = 0;

        // Knuth Shuffle
        for (int i = this.cards.Count - 1; i >= 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (this.cards[j], this.cards[i]) = (this.cards[i], this.cards[j]);
        }
    }

    public T GetNextCard()
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

    public T Peek(int index)
    {
        return this.cards[index];
    }
}
